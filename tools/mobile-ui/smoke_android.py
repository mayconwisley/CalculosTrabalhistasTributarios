"""Percursos Android reais via ADB/UIAutomator, sem dependências Python externas.

Usa exclusivamente o pacote de testes (.uitests); não apaga dados do aplicativo de produção.
As evidências e o logcat ficam no diretório --artifacts, inclusive em falha.
"""
import argparse
import os
import shutil
import json
import re
import subprocess
import time
import unicodedata
import xml.etree.ElementTree as ET
from pathlib import Path


def normalizar(texto):
    return "".join(c for c in unicodedata.normalize("NFD", texto).casefold() if unicodedata.category(c) != "Mn")


class Android:
    def __init__(self, adb, serial, package, artifacts):
        self.comando = [adb] + (["-s", serial] if serial else [])
        self.package = package
        self.artifacts = Path(artifacts)
        self.artifacts.mkdir(parents=True, exist_ok=True)
        self.xml = ""
        self.window_bounds = None
        self.system_bars = []

    def run(self, *args, binary=False):
        result = subprocess.run(self.comando + list(args), capture_output=True, timeout=180 if args and args[0] == "install" else 45)
        if result.returncode:
            raise RuntimeError("ADB: " + result.stdout.decode("utf-8", errors="replace") + result.stderr.decode("utf-8", errors="replace"))
        return result.stdout if binary else result.stdout.decode("utf-8", errors="replace")

    def nodes(self):
        result = self.run("shell", "uiautomator", "dump", "/sdcard/mobile-uitests.xml")
        if "dumped to:" not in result:
            self.xml = ""
            return []
        self.xml = self.run("exec-out", "cat", "/sdcard/mobile-uitests.xml")
        start = self.xml.find("<?xml")
        if start < 0:
            return []  # Splash e transições podem não fornecer uma hierarquia ainda.
        try:
            nodes = list(ET.fromstring(self.xml[start:]).iter("node"))
            self.window_bounds = list(map(int, re.findall(r"-?\d+", nodes[0].get("bounds", "")))) if nodes else None
            self.system_bars = [list(map(int, re.findall(r"-?\d+", n.get("bounds", "")))) for n in nodes
                                if n.get("resource-id") in ["android:id/navigationBarBackground", "android:id/statusBarBackground"]]
            if any(n.get("resource-id") == "android:id/aerr_close" for n in nodes):
                titles = [n.get("text") for n in nodes if n.get("resource-id") == "android:id/alertTitle"]
                raise AssertionError("Android apresentou ANR: " + "; ".join(titles))
            return nodes
        except ET.ParseError:
            return []

    @staticmethod
    def matches(node, value):
        wanted = normalizar(value)
        return any(normalizar(node.get(a, "")) == wanted for a in ["text", "content-desc"]) or normalizar(node.get("resource-id", "")) == wanted or normalizar(node.get("resource-id", "")).endswith("/" + wanted)

    def find(self, value, timeout=40, scroll=False):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            nodes = self.nodes()
            for node in nodes:
                if node.get("package") == self.package and self.matches(node, value) and node.get("enabled") != "false" and self.tappable(node):
                    return node
            if scroll:
                self.swipe()
            else:
                time.sleep(0.3)
        raise AssertionError(f"Elemento não encontrado: {value}; textos: {[n.get('text') or n.get('content-desc') for n in nodes]}")

    def tappable(self, node):
        bounds = list(map(int, re.findall(r"-?\d+", node.get("bounds", ""))))
        if len(bounds) != 4 or bounds[2] <= bounds[0] or bounds[3] <= bounds[1]:
            return False
        x, y = (bounds[0] + bounds[2]) // 2, (bounds[1] + bounds[3]) // 2
        if self.window_bounds and len(self.window_bounds) == 4:
            left, top, right, bottom = self.window_bounds
            if not (left <= x < right and top <= y < bottom):
                return False
        return not any(len(b) == 4 and b[0] <= x < b[2] and b[1] <= y < b[3] for b in self.system_bars)

    def tap_node(self, node):
        if not self.tappable(node):
            raise AssertionError("Controle fora da área tocável: " + str(node.attrib))
        x1, y1, x2, y2 = map(int, re.findall(r"-?\d+", node.get("bounds", "")))
        self.run("shell", "input", "tap", str((x1 + x2) // 2), str((y1 + y2) // 2))

    def tap(self, value, **kwargs):
        self.tap_node(self.find(value, **kwargs))

    def text(self, value, text, placeholder=None, **kwargs):
        self.tap(value, **kwargs)
        if self.find(value).get("focused") != "true":
            raise AssertionError("A entrada não recebeu foco antes da digitação: " + value)
        self.run("shell", "input", "keycombination", "113", "29")  # Ctrl+A: substitui em uma operação.
        self.run("shell", "input", "keyevent", "67")
        if text:
            self.run("shell", "input", "text", text.replace(" ", "%s"))
        # O ADB pode retornar antes de a UI consumir todas as teclas enfileiradas.
        deadline = time.monotonic() + 90
        while time.monotonic() < deadline:
            current = self.value(value)
            if current == text or (not text and current == placeholder):
                return
            time.sleep(0.3)
        raise AssertionError(f"Digitação incompleta em {value}: {current!r}; esperado {text!r}")

    def value(self, identifier, **kwargs):
        # O MAUI acrescenta o rótulo ao texto acessível de entradas monetárias e de período.
        return self.find(identifier, **kwargs).get("text", "").rsplit(", ", 1)[-1]

    def swipe(self, down=False):
        size = self.run("shell", "wm", "size")
        width, height = map(int, re.findall(r"(\d+)x(\d+)", size)[-1])
        y1, y2 = int(height * 0.72), int(height * 0.28)
        if down:
            y1, y2 = y2, y1
        self.run("shell", "input", "swipe", str(width // 2), str(y1), str(width // 2), str(y2), "300")

    def keyboard_visible(self):
        state = self.run("shell", "dumpsys", "input_method")
        # O serviço pode manter a input view preparada mesmo com sua janela escondida.
        return bool(re.search(r"\bmWindowVisible=true\b", state) and re.search(r"\bmIsInputViewShown=true\b", state))

    def hide_keyboard(self):
        if self.keyboard_visible():
            self.back()

    def finish_keyboard(self, field):
        for _ in range(12):
            if any(self.matches(n, field) and self.tappable(n) for n in self.nodes()):
                break
            self.swipe(down=True)
        self.tap(field, scroll=True)
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            if self.keyboard_visible():
                break
            time.sleep(0.3)
        else:
            raise AssertionError("O teclado virtual deve abrir no último campo")
        self.screenshot("08b-teclado-concluir")
        self.run("shell", "input", "keyevent", "66")
        deadline = time.monotonic() + 20
        while time.monotonic() < deadline:
            if not self.keyboard_visible():
                break
            time.sleep(0.3)
        else:
            raise AssertionError("Concluir deve fechar o teclado")
        assert self.find(field).get("focused") != "true", "Concluir deve liberar o foco da última entrada"
        self.screenshot("08c-teclado-concluido")

    def back(self):
        self.run("shell", "input", "keyevent", "4")

    def screenshot(self, name):
        try:
            self.nodes()
        except AssertionError:
            pass  # A hierarquia do ANR já foi capturada; preserve a falha original do percurso.
        (self.artifacts / (name + ".png")).write_bytes(self.run("exec-out", "screencap", "-p", binary=True))
        (self.artifacts / (name + ".xml")).write_text(self.xml, encoding="utf-8")

    def launch(self):
        resolved = self.run("shell", "cmd", "package", "resolve-activity", "--brief", self.package)
        component = next((line.strip() for line in resolved.splitlines() if line.startswith(self.package + "/")), None)
        if component is None:
            raise AssertionError("Atividade principal do pacote de testes não encontrada")
        self.run("shell", "am", "start", "-W", "-n", component)
        deadline = time.monotonic() + 180
        while time.monotonic() < deadline:
            nodes = self.nodes()
            for label in ["Pular", "Entendi"]:
                target = next((n for n in nodes if self.matches(n, label)), None)
                if target is not None:
                    self.tap_node(target)
                    break
            if any(self.matches(n, "BuscaInicio") for n in nodes):
                return
            time.sleep(0.5)
        raise AssertionError("Aplicativo não chegou à tela inicial")

    def open_calculator(self, query, title, calculate="CalcularTrabalhista"):
        self.text("BuscaInicio", query)
        self.screenshot("busca-antes-teclado")
        self.hide_keyboard()  # Com teclado físico, Back navegaria para fora da página.
        self.screenshot("busca-apos-teclado")
        self.tap(title, scroll=True)
        marcador = "Do bruto ao líquido" if calculate == "CalcularTributaria" else calculate
        deadline = time.monotonic() + 45
        while time.monotonic() < deadline:
            nodes = self.nodes()
            if any(self.matches(n, marcador) or self.matches(n, "Recuperar") for n in nodes):
                return
            time.sleep(0.3)
        raise AssertionError("A calculadora não abriu nem ofereceu recuperação do rascunho")

    def ensure_calculator(self, query, title, calculate="CalcularTrabalhista"):
        marcador = "Do bruto ao líquido" if calculate == "CalcularTributaria" else calculate
        deadline = time.monotonic() + 180
        while time.monotonic() < deadline:
            nodes = self.nodes()
            recover = next((n for n in nodes if n.get("package") == self.package and self.matches(n, "Recuperar")), None)
            if recover is not None:
                self.tap_node(recover)
            elif any(self.matches(n, marcador) and n.get("enabled") == "true" for n in nodes):
                return
            elif any(self.matches(n, "BuscaInicio") for n in nodes):
                self.open_calculator(query, title, calculate)
            else:
                time.sleep(0.5)
        raise AssertionError("Formulário não reapareceu após alterar a configuração do aparelho")

    def home(self):
        for _ in range(4):
            nodes = self.nodes()
            if any(n.get("text") == "Calculadoras" for n in nodes):
                break
            self.back()
        for _ in range(15):
            if any(self.matches(n, "BuscaInicio") for n in self.nodes()):
                return
            self.swipe(down=True)
        raise AssertionError("Não foi possível voltar à busca inicial")

    def prompt(self, title, text, submit):
        self.find(title)
        entry = next(n for n in self.nodes() if n.get("package") == self.package and n.get("class") == "android.widget.EditText")
        self.tap_node(entry)
        self.run("shell", "input", "keycombination", "113", "29")
        self.run("shell", "input", "keyevent", "67")
        self.run("shell", "input", "text", text.replace(" ", "%s"))
        self.tap(submit)

    def tap_card(self, title, action):
        deadline = time.monotonic() + 45
        while time.monotonic() < deadline:
            self.nodes()
            if self.xml:
                tree = ET.fromstring(self.xml[self.xml.find("<?xml"):])
                parents = {child: parent for parent in tree.iter() for child in parent}
                named = next((n for n in tree.iter("node") if self.matches(n, title)), None)
                while named is not None:
                    target = next((n for n in named.iter("node") if self.matches(n, action)), None)
                    if target is not None and self.tappable(target):
                        base = title.removesuffix(" (cópia)")
                        titles = [n for n in named.iter("node") if n.get("class") == "android.widget.TextView" and n.get("text") in [base, base + " (cópia)"]]
                        if len(titles) != 1:
                            break  # Não use uma ação de outro cartão quando o desejado estiver fora da tela.
                        self.tap_node(target)
                        return
                    named = parents.get(named)
            self.swipe()
        raise AssertionError(f"Ação {action} não encontrada no cálculo {title}")

    def overflow(self, item):
        deadline = time.monotonic() + 40
        while time.monotonic() < deadline:
            nodes = self.nodes()
            target = next((n for n in nodes if n.get("package") == self.package and self.tappable(n) and any(self.matches(n, v) for v in ["More options", "Mais opções"])), None)
            if target is not None:
                break
            time.sleep(0.3)
        else:
            raise AssertionError("Menu de opções não encontrado")
        self.tap_node(target)
        self.tap(item)


def smoke(app):
    print("Abrindo aplicativo...", flush=True)
    app.launch()
    app.open_calculator("recibo abono", "Férias")
    print("Preenchendo exemplo de férias...", flush=True)
    app.screenshot("00-formulario-claro")
    app.tap("Escolher Competência do pagamento (mm/aaaa)", scroll=True)
    app.text("AnoCompetencia", "2026")
    app.hide_keyboard()
    app.tap("MesCompetencia")
    app.tap("janeiro")
    app.tap("AplicarCompetencia")
    assert app.value("Campo_competencia") == "01/2026"
    app.tap("Escolher Início do gozo (dd/mm/aaaa, opcional)", scroll=True)
    app.find("OK")
    app.screenshot("00b-calendario-nativo")
    app.tap("OK")
    selected_date = app.value("Campo_inicio")
    assert re.fullmatch(r"\d{2}/\d{2}/\d{4}", selected_date), "Confirmar o calendário deve preencher a data, inclusive quando ela é a data inicial do seletor: " + selected_date
    app.screenshot("00c-periodos-preenchidos")
    app.overflow("Preencher exemplo")
    app.tap("Preencher")
    app.tap("IrAoResultado", timeout=40)
    app.find("SEU RESULTADO", timeout=30)
    app.screenshot("01-resultado-claro")
    app.tap("CompartilharPdf", scroll=True)
    deadline = time.monotonic() + 40
    while time.monotonic() < deadline:
        if any(n.get("package") in ["android", "com.android.intentresolver"] for n in app.nodes()):
            break
        time.sleep(0.5)
    else:
        raise AssertionError("O compartilhamento de PDF não abriu a seleção de aplicativo")
    app.screenshot("01b-compartilhamento-pdf")
    app.back()
    app.tap("Expandir Demonstrativo", scroll=True)
    app.find("Proventos", scroll=True)
    app.tap("IrAoResultado")
    app.tap("Expandir Memória de cálculo", scroll=True)
    app.find("Recolher Memória de cálculo")
    app.screenshot("02-memoria")
    # Editar uma data inválida abre a seção e leva foco ao campo correspondente.
    for _ in range(20):
        app.swipe(down=True)
        if any(app.matches(n, "Campo_inicio") for n in app.nodes()):
            break
    app.text("Campo_inicio", "31/02/2024", scroll=True)
    app.hide_keyboard()
    app.tap("CalcularTrabalhista")
    node = app.find("Campo_inicio")
    assert node.get("focused") == "true", "O primeiro campo inválido deve receber foco"
    app.screenshot("03-erro-no-campo")
    app.text("Campo_inicio", "01/10/2026")
    app.hide_keyboard()
    # Persistir e recuperar após encerramento do processo, sem usar Salvar.
    app.text("Campo_salario", "4567,89", scroll=True)
    app.run("shell", "input", "keyevent", "66")
    assert app.find("Campo_medias").get("focused") == "true", "Próximo deve avançar para a próxima entrada disponível"
    app.screenshot("03b-teclado-proximo")
    app.text("Campo_medias", "0,00")  # O zero foi limpo ao receber foco; o rascunho aceita formulários incompletos.
    app.hide_keyboard()
    time.sleep(2)
    app.run("shell", "input", "keyevent", "3")  # Stopped também solicita persistência
    time.sleep(1)
    app.run("shell", "am", "force-stop", app.package)
    app.launch()
    app.open_calculator("recibo abono", "Férias")
    app.tap("Recuperar", timeout=20)
    value = app.value("Campo_salario", scroll=True)
    assert value in ["4.567,89", "4567,89"], f"Rascunho recuperado incorretamente: {value}"
    app.screenshot("04-rascunho-recuperado")
    # O histórico/comparativo usa uma fixture válida, independente dos campos de foco/rascunho.
    app.overflow("Preencher exemplo")
    app.tap("Preencher")
    app.tap("Salvar")
    app.prompt("Salvar cálculo", "UI Ferias A", "Salvar")
    app.tap("Entendi")
    # Tema, tela reduzida e fonte ampliada no emulador isolado.
    app.run("shell", "cmd", "uimode", "night", "yes")
    app.run("shell", "wm", "size", "840x1680")
    app.run("shell", "wm", "density", "420")
    app.run("shell", "settings", "put", "system", "font_scale", "1.3")
    app.ensure_calculator("recibo abono", "Férias")
    # A conferência geométrica usa novamente a fixture completa; o rascunho já foi verificado acima.
    app.overflow("Preencher exemplo")
    app.tap("Preencher")
    app.tap("IrAoResultado", timeout=40)
    app.find("SEU RESULTADO", timeout=30)
    app.screenshot("05-resultado-escuro-320dp")
    app.run("shell", "settings", "put", "system", "accelerometer_rotation", "0")
    app.run("shell", "settings", "put", "system", "user_rotation", "1")
    app.tap("IrAoResultado")
    app.screenshot("05b-resultado-paisagem")
    app.run("shell", "settings", "put", "system", "user_rotation", "0")
    app.ensure_calculator("recibo abono", "Férias")
    # O descarte remove a cópia automática e restaura o formulário.
    app.overflow("Descartar rascunho")
    app.tap("Descartar")
    for _ in range(15):
        app.swipe(down=True)
        if any(app.matches(n, "Campo_salario") for n in app.nodes()):
            break
    assert app.value("Campo_salario", scroll=True) == "0,00"
    app.screenshot("06-rascunho-descartado")
    app.home()
    app.open_calculator("verbas desligamento", "Rescisão")
    app.overflow("Preencher exemplo")
    app.tap("Preencher")
    app.tap("IrAoResultado", timeout=40)
    app.find("SEU RESULTADO", timeout=30)
    app.screenshot("07-rescisao-escuro-320dp")
    app.home()
    app.open_calculator("duas modalidades", "Simulação tributária", "CalcularTributaria")
    app.overflow("Preencher exemplo")
    app.tap("Preencher")
    app.overflow("Ir ao resultado")
    app.find("SEU RESULTADO", timeout=30)
    app.screenshot("08-tributacao-escuro-320dp")
    app.finish_keyboard("Dependentes")
    app.home()
    app.text("BuscaInicio", "", placeholder="Buscar cálculos e tabelas")
    app.hide_keyboard()
    app.tap("Ver o histórico", scroll=True)
    app.find("BuscaHistorico")
    app.tap_card("UI Ferias A", "Duplicar")
    for _ in range(5):
        app.swipe(down=True)
    app.find("2 de 2 cálculos")
    app.screenshot("09-historico-paginado")
    app.tap_card("UI Ferias A (cópia)", "Selecionar / remover da comparação")
    app.tap_card("UI Ferias A", "Selecionar / remover da comparação")
    for _ in range(5):
        app.swipe(down=True)
    app.tap("Comparar selecionados")
    app.find("Comparação", timeout=40)
    deadline = time.monotonic() + 40
    while time.monotonic() < deadline:
        nodes = app.nodes()
        assert not any("não pôde ser recalculado" in n.get("text", "") for n in nodes), "Os exemplos salvos devem permitir comparar os resultados"
        if any(n.get("text", "").startswith("Diferença:") for n in nodes):
            break
        app.swipe()
    else:
        raise AssertionError("A comparação deve apresentar diferenças numéricas, não apenas abrir a página")
    app.screenshot("10-comparacao-historico")


def verificar_pacote(apk, adb, esperado):
    sdk = Path(os.environ.get("ANDROID_HOME") or os.environ.get("ANDROID_SDK_ROOT") or Path(shutil.which(adb) or adb).resolve().parent.parent)
    executaveis = sorted(sdk.glob("build-tools/*/aapt2" + (".exe" if os.name == "nt" else "")))
    if not executaveis:
        raise RuntimeError("aapt2 não encontrado no Android SDK; o pacote deve ser verificado antes de instalar")
    result = subprocess.run([str(executaveis[-1]), "dump", "badging", apk], check=True, capture_output=True, timeout=30)
    match = re.search(r"package: name='([^']+)'", result.stdout.decode("utf-8", errors="replace"))
    if match is None or match.group(1) != esperado:
        raise ValueError("O teste aceita somente APK com o identificador isolado terminado em .uitests")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--apk", required=True)
    parser.add_argument("--adb", default="adb")
    parser.add_argument("--serial")
    parser.add_argument("--reuse-installed", action="store_true", help="Diagnóstico: retoma somente o pacote .uitests já instalado, sem limpar dados")
    parser.add_argument("--artifacts", default="TestResults/android-ui")
    args = parser.parse_args()
    app = Android(args.adb, args.serial, "br.com.mayconwisley.calculostrabalhistastributarios.uitests", args.artifacts)
    verificar_pacote(args.apk, args.adb, app.package)
    print("Aguardando Android concluir o boot...", flush=True)
    deadline = time.monotonic() + 600
    while time.monotonic() < deadline:
        try:
            if app.run("shell", "getprop", "sys.boot_completed").strip() == "1":
                break
        except RuntimeError:
            pass
        time.sleep(1)
    else:
        raise AssertionError("Android não concluiu o boot")
    # Somente este pacote descartável é limpo. O APK é compilado com ApplicationId=.uitests.
    if not args.reuse_installed:
        app.run("install", "--no-incremental", "-r", args.apk)
        app.run("shell", "pm", "clear", app.package)
    started = time.monotonic()
    original_font = app.run("shell", "settings", "get", "system", "font_scale").strip()
    original_size = app.run("shell", "wm", "size")
    original_density = app.run("shell", "wm", "density")
    original_night = app.run("shell", "cmd", "uimode", "night")
    original_rotation = app.run("shell", "settings", "get", "system", "user_rotation").strip()
    original_accelerometer = app.run("shell", "settings", "get", "system", "accelerometer_rotation").strip()
    try:
        app.run("shell", "cmd", "uimode", "night", "no")
        app.run("shell", "wm", "size", "1080x2400")
        app.run("shell", "wm", "density", "420")
        app.run("shell", "settings", "put", "system", "font_scale", "1.0")
        smoke(app)
        (app.artifacts / "resultado.json").write_text(json.dumps({"sucesso": True, "duracao_segundos": round(time.monotonic() - started, 2)}, indent=2), encoding="utf-8")
        print("Percursos Android concluídos.")
    except Exception:
        app.screenshot("falha")
        try:
            (app.artifacts / "ultimo-anr.txt").write_text(app.run("shell", "dumpsys", "activity", "lastanr"), encoding="utf-8")
        except (RuntimeError, subprocess.TimeoutExpired) as error:
            (app.artifacts / "diagnostico-indisponivel.txt").write_text(str(error), encoding="utf-8")
        raise
    finally:
        (app.artifacts / "logcat.txt").write_text(app.run("logcat", "-d", "-t", "2000"), encoding="utf-8")
        app.run("shell", "am", "force-stop", app.package)
        app.run("shell", "settings", "put", "system", "font_scale", original_font if original_font != "null" else "1.0")
        app.run("shell", "wm", "size", re.search(r"Override size: (\d+x\d+)", original_size).group(1) if "Override size:" in original_size else "reset")
        app.run("shell", "wm", "density", re.search(r"Override density: (\d+)", original_density).group(1) if "Override density:" in original_density else "reset")
        app.run("shell", "cmd", "uimode", "night", next((mode for mode in ["yes", "no", "auto", "custom"] if mode in original_night), "auto"))
        app.run("shell", "settings", "put", "system", "user_rotation", original_rotation if original_rotation != "null" else "0")
        app.run("shell", "settings", "put", "system", "accelerometer_rotation", original_accelerometer if original_accelerometer != "null" else "1")


if __name__ == "__main__":
    main()
