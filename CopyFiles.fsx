open System.IO

let args = fsi.CommandLineArgs |> Seq.skip 1

let (baseDirIndex, baseDir) =
    args
    |> Seq.pairwise
    |> Seq.indexed
    |> Seq.tryPick (function i, ("--baseDir", p) -> Some (i, p) | _ -> None)
    |> Option.defaultValue (-1, "./")

let (outDirIndex, outDir) =
    args
    |> Seq.pairwise
    |> Seq.indexed
    |> Seq.tryPick (function i, ("--outDir", p) -> Some (i, p) | _ -> None)
    |> function
    | None -> System.ArgumentException() |> raise
    | Some arg -> arg

let excludeIndices = [baseDirIndex; baseDirIndex + 1; outDirIndex; outDirIndex + 1]

let files =
    args
    |> Seq.indexed
    |> Seq.where (fun (i, _) -> excludeIndices |> Seq.contains i |> not)
    |> Seq.map snd
    |> Seq.where (_.Contains("--") >> not)
    |> Seq.collect _.Split([|';'|])
    |> Seq.map Path.GetFullPath

let relPath path =
    let r = Path.GetRelativePath(baseDir, path)
    if r.Contains("..") |> not then
        r
    else
        let r = Path.GetRelativePath("./", path)
        let i = r.LastIndexOf("..")
        let r = if i > 0 then r.Remove(0, i + 3) else r
        r

for file in files do
    let op = Path.GetFullPath(Path.Combine(outDir, relPath file))
    printfn "%s -> %s" file op
    let targetDir = Path.GetDirectoryName(op)

    if Directory.Exists(targetDir) |> not then
        Directory.CreateDirectory(targetDir) |> ignore

    File.Copy (file, op, true)
