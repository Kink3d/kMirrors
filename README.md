# kPackageTemplate
### Package distribution template for Unity.

kPackageTemplate is a template for defining a Github repository as a package for Unity's Package Manager. Follow instructions below for defining your package from this template.

## Defining a Package from this template
- Create a Github repository from this template.
- Open `package.json` and set all fields
   - **version** is for versioning this package. See  [Semantic Versioning](http://semver.org/spec/v2.0.0.html) for more information.
   - **unity** defines the minimum Editor version required. For example, any 2019.3 Editor build would be just `2019.3`.
   - You can define dependencies in **dependencies**. However, any dependencies to custom packages from Git will not automatically download (as Unity official packages will). They will also have to be added to the project manifest by the user. In this case a **references** entry only blocks this package from resolving unless the referenced package is present.
- Update assembly definitions
   - This template includes the three most common assembly definitions (**Runtime**, **Editor** and **EditorTests**). Assembly definitions are required for package compilation but the supplied definitions are just the most common. You can redefine them if you like.
   - These `asmdef` files need to be renamed, both the asset filename as well as the **name** field on the asset (these should match, and be relevent to the package name).
   - Ensure the assembly references are correct; both **Editor** and **EditorTests** assemblies should reference **Runtime** assembly. **EditorTests** also references the **TestRunner** as well as **NUnit**.
- Update `CHANGELOG.md`. See [Keep a Changelog](http://keepachangelog.com/en/1.0.0/) for more information.
- Update this `README.md` to reflect the contents of your package.
- Update `LICENSE`. Supplied license is MIT, this is only a suggestion.

## Adding the Package to a Project
- Open your project manifest file (`MyProject/Packages/manifest.json`).
- Add `"com.author.packagename": "https://github.com/MyGithubUserName/MyRepository.git"` to the `dependencies` list (replacing package name and repository information).
- Open or focus on Unity Editor to resolve packages.