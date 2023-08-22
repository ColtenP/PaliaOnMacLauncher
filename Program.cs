// See https://aka.ms/new-console-template for more information

using PaliaOnMacLauncher;

var commandArgs = Environment.GetCommandLineArgs().ToList();
var installationPath = Environment.GetEnvironmentVariable("INSTALLATION_PATH") ?? Path.Combine("C:", "Program Files", "Palia");
var patchManifestUrl = Environment.GetEnvironmentVariable("PATCH_MANIFEST_URL") ?? "https://update.palia.com/manifest/PatchManifest.json";
var localZipFile = Environment.GetEnvironmentVariable("LOCAL_ZIP_FILE");

Console.WriteLine("Installation Path: {0}", installationPath);
Console.WriteLine("Patch Manifest Url: {0}", patchManifestUrl);
if (localZipFile is not null) Console.WriteLine("Local Zip File: {0}", localZipFile);

var launcher = new Launcher(installationPath, patchManifestUrl, localZipFile);
await launcher.Start();
