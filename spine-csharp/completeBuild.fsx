// include Fake lib
#r @"packages\FAKE\tools\FakeLib.dll"

open Fake
open Fake.AssemblyInfoFile


RestorePackages()

// Directories
let buildDir  = @".\build\"
let testDir   = @".\test\"
let deployDir = @".\deploy\"
let packagesDir = @".\packages"


// Project info
let authors = ["Andrea Magnorsky";"Andrew O'Connor"; "Esoteric Software"]
let projectName = "SpineRuntime"
type ProjectInfo = { 
    Name: string;
    Description: string; 
    Version: string;
  }
let info = {
  Name=projectName;
  Description =  "Android runtime for the C# runtime of Spine is a 2D skeletal animation tool for game development and other animation projects";
  Version = if isLocalBuild then "0.2-local" else "0.2."+buildVersion
}

// Targets
Target "Clean" (fun _ ->
    CleanDirs [buildDir; testDir; deployDir]
)

Target "SetVersions" (fun _ ->
    CreateCSharpAssemblyInfo "./Properties/AssemblyInfo.cs"
        [Attribute.Title info.Name
         Attribute.Description info.Description
         Attribute.Guid "c1dcbc84-7e8b-46f3-a253-9d9527434dee"         
         Attribute.Version info.Version
         Attribute.FileVersion info.Version]
)


Target "Compile" (fun _ ->
    MSBuildRelease 
    !! @"spine-csharp.sln"
      |> MSBuildRelease "" "Build"      
      |> Log "AppBuild-Output: "
)

Target "CompileTest" (fun _ ->
    !! @"**\Test*.csproj"
      |> MSBuildDebug testDir "Build"
      |> Log "TestBuild-Output: "
)

Target "NUnitTest" (fun _ ->
    !! (testDir + @"\Test*.dll")
      |> NUnit (fun p ->
                 {p with
                   DisableShadowCopy = true;
                   OutputFile = testDir + @"TestResults.xml"})

)
let nugetPath = ".nuget/NuGet.exe"

Target "CreatePackage" (fun _ ->    
    
    NuGet (fun p -> 
        {p with
            Authors = authors
            Project = projectName            
            Version = info.Version
            Description = info.Description                                           
            OutputPath = deployDir            
            ToolPath = nugetPath
            Summary = info.Description    
            Tags = "2D skeletal-animation runtime-Spine"                               
            PublishUrl = getBuildParamOrDefault "nugetrepo" ""
            AccessKey = getBuildParamOrDefault "nugetkey" ""            
            Publish = hasBuildParam "nugetkey"  
            }) 
            "nuget/Spine.nuspec"
)

Target "AndroidPack" (fun _ ->        
    NuGet (fun p -> 
        {p with
            Authors = authors
            Project = projectName+".Android"
            Version = info.Version
            Description = info.Description                                           
            OutputPath = deployDir            
            ToolPath = nugetPath
            Summary = info.Description            
            Tags = "2D skeletal-animation runtime-Spine"           
            AccessKey = getBuildParamOrDefault "nugetkey" ""
            Publish = hasBuildParam "nugetkey"
            PublishUrl = getBuildParamOrDefault "nugetUrl" ""
            }) 
            "nuget/Spine.Android.nuspec"
)


// Dependencies
"Clean"
  ==> "SetVersions"
  ==> "Compile"
  ==> "CompileTest"
  ==> "NUnitTest"
  ==> "CreatePackage"

"Clean"
  ==> "SetVersions"
  ==> "Compile"
  ==> "AndroidPack"

// start build
RunTargetOrDefault "CreatePackage"