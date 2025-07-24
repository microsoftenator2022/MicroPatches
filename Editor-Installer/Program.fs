open System.IO
open System.IO.Compression
open System.Reflection

open Mono.Cecil

open Strings

let getGameDir () =
    use log = Path.Join(System.Environment.ExpandEnvironmentVariables rtAppData, "Player.log") |> File.OpenText
    let rec getLines () = seq {
        match log.ReadLine() |> Option.ofObj with
        | Some l ->
            yield l
            yield! getLines ()
        | None -> ()
    }

    getLines()
    |> Seq.map (fun line -> gamePathRegex.Match line)
    |> Seq.pick (fun m -> m.Groups[1].Value |> Option.ofObj)

let getEditorZip () =
    new ZipArchive(
        Assembly.GetExecutingAssembly().GetManifestResourceStream(
            assemblyResourcePrefix + editorZipFileName))
    
let getUmmZip () =
    new ZipArchive(
        Assembly.GetExecutingAssembly().GetManifestResourceStream(
            assemblyResourcePrefix + ummFileName))

let getZips () =
    getEditorZip(), getUmmZip()

let unhideBodyPartInspectors (codeAssPath : string) =
    printf "Unhiding BodyPart inspector fields... "

    use asmResolver = new DefaultAssemblyResolver()
    
    rtAssembliesDir
    |> asmResolver.AddSearchDirectory

    Path.Join(getGameDir(), "WH40KRT_Data", "Managed")
    |> asmResolver.AddSearchDirectory

    use moduleDef = ModuleDefinition.ReadModule(codeAssPath, ReaderParameters(AssemblyResolver = asmResolver))
    let bodyPartType = moduleDef.Types |> Seq.find (fun t -> t.FullName = bodyPartFullTypeName)

    for f in bodyPartType.Fields do
        f.CustomAttributes
        |> Seq.tryFind (fun attr -> attr.AttributeType.Name = hideIfAttributeName)
        |> function
        | Some attr ->
            f.CustomAttributes.Remove attr |> ignore
            moduleDef.Write $"{codeAssPath}_new"
        | None -> ()

    printfn "Done"

let install templatePath =
    let editorZip, ummZip = getZips ()

    for e in editorZip.Entries do
        if e.Length <> 0 then
            printfn "extracting %s" e.FullName
            if File.Exists e.FullName then
                File.Delete e.FullName

            let dir = Path.GetDirectoryName e.FullName

            if not (dir |> Directory.Exists) then
                Directory.CreateDirectory(dir) |> ignore

            e.ExtractToFile e.FullName

    if File.Exists dnlibPath then
        printfn "deleting %s" dnlibPath
        File.Delete dnlibPath

    for f in Directory.EnumerateFiles ummPath do
        printfn "deleting %s" f
        File.Delete f

    for e in ummZip.Entries do
        if e.Length <> 0 then
            let p = Path.Join("Assets", e.FullName)
            printfn "extracting %s"p
            e.ExtractToFile p

    for f in Directory.EnumerateFiles("Library/ScriptAssemblies")
        |> Seq.where (Path.GetFileName >> _.StartsWith("UnityModManager")) do
        printfn "deleting %s" f
        File.Delete f

    if File.Exists projectDependenciesGraphPath then
        printfn "deleting %s" projectDependenciesGraphPath

    let codeAssPath = Path.Join(rtAssembliesDir, codeDllFileName)

    unhideBodyPartInspectors codeAssPath

    if File.Exists $"{codeAssPath}_new" then
        File.Copy (codeAssPath, $"{codeAssPath}_original._", true)
        
        File.Move($"{codeAssPath}_new", codeAssPath, true)

    printfn "Install completed successfully"

[<EntryPoint>]
let main args =
    Assembly.GetExecutingAssembly().GetName().Version
    |> printfn "Version: %A"

    let templatePath = args |> Array.tryHead

    try
        match templatePath with
        | Some path ->
            if Directory.Exists path then
                System.Environment.CurrentDirectory <- path |> Path.GetFullPath
            else
                DirectoryNotFoundException $"Template directory not found at '{path}'" |> raise
        | None -> printfn "Using current directory"

        if checkTemplateFiles |> Seq.forall File.Exists |> not
            || checkTemplateDirectories |> Seq.forall Directory.Exists |> not then
            System.Environment.CurrentDirectory
            |> sprintf "'%s' does not look like a mod template"
            |> System.ArgumentException
            |> raise

        install ()
    with e ->
        printfn "Exception occured"
        printfn "%A" e

    printfn "Press any key to exit"

    System.Console.ReadKey() |> ignore

    0
