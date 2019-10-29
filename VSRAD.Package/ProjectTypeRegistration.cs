using Microsoft.VisualStudio.ProjectSystem.VS;
using VSRAD.Package;
using DebugConstants = VSRAD.Deborgar.Constants;

[assembly: ProjectTypeRegistration(
    projectTypeGuid: Constants.ProjectTypeId,
    displayName: "#1",
    displayProjectFileExtensions: "#2",
    defaultProjectExtension: Constants.ProjectFileExtension,
    language: DebugConstants.LanguageName,
    resourcePackageGuid: Constants.PackageId,
    PossibleProjectExtensions = Constants.ProjectFileExtension)]
