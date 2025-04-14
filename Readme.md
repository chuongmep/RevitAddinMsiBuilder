
# 🔧 Revit CLI MSI Installer

<p align="center">
  <img src="./docs/3717068.png" alt="AddinMsiBuilder" style="max-height: 50px; width: auto;" />
</p>


Revit CLI MSI Installer is a lightweight command-line tool that simplifies packaging Revit Add-ins into MSI installers. It helps Revit developers automate distribution across multiple Revit versions with consistent folder structures and deployment formats.

You want Desktop App ? Visit 👉 [RevitMsiBuilderWPF](https://github.com/chuongmep/RevitMsiBuilderWPF)

# 💡 Why This Tool?
Manually creating MSI installers for Revit add-ins is repetitive and error-prone. This tool automates that process, giving you:
- Cleaner release pipelines
- Better team collaboration
- Less time spent on packaging, more on features
# ✨ Key Features

🔁 Multi-Version Support – Package your Add-in for multiple Revit versions (e.g., 2022, 2023, 2025).

📦 MSI + ZIP Output – Automatically creates MSI installer and a portable ZIP for manual installation.

🧩 Simple .addin Integration – Uses your existing .addin file for configuration.

🚀 Effortless Deployment – Ensures your Add-in gets installed to the correct %AppData%\Autodesk\Revit\Addins\{Year} folders.

# Usage

CLI tool to build MSI for Revit add-ins

```bash
Options:
  --addin-path <addin-path> (REQUIRED)
      Path to the .addin file or directory containing it
  --revit-versions <revit-versions>
      Target Revit versions (e.g., 2022,2023)
  --output-dir <output-dir>
      Output directory for MSI and ZIP
  --project-name <project-name>
      Name of the project for MSI and output file (defaults to .addin Name or RevitAddinMsi)
```

## Example Revit CLI MSI Installer project

1. Setup the project with the following structure:

```bash
D:\API\Test Revit CLI Msi\Installer\test\
├── hello.addin
├── contents\
│   ├── hello.dll
```
2. Run a Revit CLI MSI installer using the Revit CLI MSI Installer project.
```bash
dotnet run -- --addin-path "D:\API\Test Revit CLI Msi\Installer\test\hello.addin" --revit-versions 2022 2023 --output-dir "output" --project-name "MyCoolAddin"
```
3. Verify Installation:

```bash
%AppDataFolder%\Autodesk\Revit\Addins\
    ├── 2022\
    │   ├── hello.addin
    │   ├── contents\
    │   │   ├── hello.dll
    ├── 2023\
    │   ├── hello.addin
    │   ├── contents\
    │   │   ├── hello.dll
```
## Template .Addin file

- Usage for Command
```xml
<?xml version="1.0" encoding="utf-8" standalone="no"?>
<RevitAddIns>
  <AddIn Type="Command">
    <Name>Test</Name>
    <Assembly>contents\hello.dll</Assembly>
    <AddInId>e1f2c3d4-e5f6-7a8b-9a0b-c1d2e3f4g5h6</AddInId>
    <FullClassName>Test.TestCommand</FullClassName>
    <VendorId>Company</VendorId>
    <VendorDescription>My Company</VendorDescription>
  </AddIn>
</RevitAddIns>
```
- Usage for Application With Ribbon
```xml
<?xml version="1.0" encoding="utf-8"?>
<RevitAddIns>
  <AddIn Type="Application">
    <Name>Test</Name>
    <Assembly>contents\hello.dll</Assembly>
    <AddInId>12345678-1234-1234-1234-1234567890AB</AddInId>
    <FullClassName>Test.TestApp</FullClassName>
    <VendorId>ADSK</VendorId>
  </AddIn>
</RevitAddIns>
```
