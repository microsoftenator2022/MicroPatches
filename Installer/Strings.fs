module Strings

let assemblyResourcePrefix = "MicroPatches_Editor_Installer."
let editorZipFileName = "MicroPatches-Editor.zip"
let ummFileName = "UnityModManager.zip"
let dnlibPath = "Assets/Libs/dnlib.dll"
let ummPath = "Assets/UnityModManager"
let projectDependenciesGraphPath = "Library/APIUpdater/project-dependencies.graph"
let rtAssembliesDir = "Assets/RogueTraderAssemblies"
let rtAppData = @"%LocalAppData%Low\Owlcat Games\Warhammer 40000 Rogue Trader"
let gamePathRegex =
    @"^Mono path\[0\] = '(.*?)/WH40KRT_Data/Managed'$"
    |> System.Text.RegularExpressions.Regex 
let codeDllFileName = "Code.dll"
let bodyPartFullTypeName = "Kingmaker.Visual.CharacterSystem.BodyPart"
let hideIfAttributeName = "HideIfAttribute"
let checkTemplateFiles = [
    "WhRtModificationTemplate-release.sln"
]

let checkTemplateDirectories = [
    "Blueprints"
    "BlueprintIndexingServer"
]
