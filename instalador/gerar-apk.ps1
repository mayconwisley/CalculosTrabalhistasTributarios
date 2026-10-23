<#
.SYNOPSIS
Gera o APK assinado do aplicativo Android de Cálculos Trabalhistas e Tributários.

.DESCRIPTION
Roda os testes do mobile, publica CalculosTrabalhistasTributarios.Mobile em Release como APK e devolve o caminho do
arquivo gerado. A versão vem da tag, como no instalador do Windows; o código de versão do Android é calculado dela
(maior × 10000 + menor × 100 + correção), para que cada versão nova atualize a anterior no aparelho.

O APK é assinado com a chave de publicação informada nas variáveis de ambiente:
  CALCULADORA_ANDROID_KEYSTORE  caminho do arquivo .keystore
  CALCULADORA_ANDROID_ALIAS     alias da chave dentro do arquivo
  CALCULADORA_ANDROID_SENHA     senha do arquivo e da chave
O Android só instala uma atualização assinada com a mesma chave da versão instalada; com outra chave, o usuário
precisa desinstalar o app e perde os cálculos salvos. Guarde o arquivo e a senha com cópia de segurança.
Não envia nada ao GitHub: a publicação é feita por publicar-release.ps1.

.EXAMPLE
.\instalador\gerar-apk.ps1 -Versao 1.20.0
#>
param(
    [Parameter(Mandatory = $true)] [string] $Versao,
    # Raiz do código-fonte a compilar. Na publicação de uma tag, é a cópia isolada (worktree) daquela tag.
    [string] $Raiz,
    [string] $Saida,
    # Só para simulação: sem a chave de publicação, assina com a chave de depuração do SDK. Esse APK não serve para distribuir.
    [switch] $PermitirChaveDeDepuracao
)

$ErrorActionPreference = 'Stop'
if ([Console]::IsOutputRedirected) { [Console]::OutputEncoding = New-Object Text.UTF8Encoding $false }
if (-not $Raiz) { $Raiz = Split-Path $PSScriptRoot -Parent }
if (-not $Saida) { $Saida = Join-Path (Split-Path $PSScriptRoot -Parent) 'artefatos' }

if ($Versao -notmatch '^(\d+)\.(\d+)\.(\d+)(-[0-9A-Za-z.-]+)?$') {
    throw "Versão inválida: '$Versao'. Use o formato 1.2.0 ou 1.2.0-beta.1."
}
if ([int]$Matches[2] -gt 99 -or [int]$Matches[3] -gt 99) {
    throw "Versão ${Versao}: o código de versão do Android comporta até 99 na versão menor e na correção."
}
$codigoVersao = [int]$Matches[1] * 10000 + [int]$Matches[2] * 100 + [int]$Matches[3]
$versaoExibida = ($Versao -split '-')[0]

$assinatura = @()
# Espaços nas pontas aparecem facilmente ao colar o caminho na janela de variáveis do Windows.
$chave = "$env:CALCULADORA_ANDROID_KEYSTORE".Trim()
if ($chave -and $env:CALCULADORA_ANDROID_ALIAS -and $env:CALCULADORA_ANDROID_SENHA) {
    if (-not (Test-Path $chave)) { throw "A chave de publicação do Android não foi encontrada em $chave (CALCULADORA_ANDROID_KEYSTORE)." }
    # As senhas seguem pelo prefixo env: e não aparecem na linha de comando nem no log.
    $assinatura = @('-p:AndroidKeyStore=true', "-p:AndroidSigningKeyStore=$chave", "-p:AndroidSigningKeyAlias=$($env:CALCULADORA_ANDROID_ALIAS.Trim())",
        '-p:AndroidSigningStorePass=env:CALCULADORA_ANDROID_SENHA', '-p:AndroidSigningKeyPass=env:CALCULADORA_ANDROID_SENHA')
}
elseif ($PermitirChaveDeDepuracao) {
    Write-Warning 'Chave de publicação do Android não configurada: o APK da simulação usa a chave de depuração e não serve para distribuir.'
}
else {
    throw 'A chave de publicação do Android não está configurada. Defina CALCULADORA_ANDROID_KEYSTORE, CALCULADORA_ANDROID_ALIAS e CALCULADORA_ANDROID_SENHA (veja docs/MAUI.md, "Publicação do APK").'
}

$projeto = Join-Path $Raiz 'CalculosTrabalhistasTributarios.Mobile\CalculosTrabalhistasTributarios.Mobile.csproj'
$publicacao = Join-Path ([IO.Path]::GetTempPath()) ("CalculosTrabalhistasTributarios-apk-" + [Guid]::NewGuid().ToString('N'))

try {
    $testes = Join-Path $Raiz 'CalculosTrabalhistasTributarios.Mobile.Tests\CalculosTrabalhistasTributarios.Mobile.Tests.csproj'
    if (Test-Path $testes) {
        Write-Host "==> Rodando os testes do mobile..."
        & dotnet test $testes -c Release --nologo | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Os testes do mobile falharam (código $LASTEXITCODE): o APK não foi gerado." }
    }

    Write-Host "==> Publicando o APK $Versao (código de versão $codigoVersao)..."
    & dotnet publish $projeto -f net10.0-android -c Release -o $publicacao --nologo `
        "-p:ApplicationDisplayVersion=$versaoExibida" "-p:ApplicationVersion=$codigoVersao" '-p:AndroidPackageFormat=apk' @assinatura | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish do Android falhou (código $LASTEXITCODE)." }

    $gerado = Get-ChildItem $publicacao -Filter '*-Signed.apk' | Select-Object -First 1
    if (-not $gerado) { throw "O APK assinado não foi encontrado em $publicacao." }
    New-Item -ItemType Directory -Force -Path $Saida | Out-Null
    $apk = Join-Path $Saida "CalculosTrabalhistasTributarios-$Versao-android.apk"
    Copy-Item -LiteralPath $gerado.FullName -Destination $apk -Force
}
finally {
    if (Test-Path $publicacao) { Remove-Item -LiteralPath $publicacao -Recurse -Force }
}

Write-Host ("==> APK gerado: {0} ({1:N1} MB)" -f $apk, ((Get-Item $apk).Length / 1MB))
$apk
