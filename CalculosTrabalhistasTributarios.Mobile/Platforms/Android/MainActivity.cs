using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace CalculosTrabalhistasTributarios.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
// Recebe os atalhos do ícone (AppActions); o Essentials lê o intent ao retomar ou ao receber um novo com a atividade aberta.
[IntentFilter(new[] { Platform.Intent.ActionAppAction }, Categories = new[] { Intent.CategoryDefault })]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnResume()
    {
        base.OnResume();
        Platform.OnResume(this);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Platform.OnNewIntent(intent);
    }
}
