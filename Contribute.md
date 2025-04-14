## Build Nuget Package 

```
donet restore
dotnet pack --configuration Release --output ./nupkgs
```

## Publish Nuget Package Online 

Local
```bash
dotnet clean
dotnet build -c Release
dotnet pack -c Release
dotnet nuget push ./nupkgs/RevitAddinMsiBuilder.1.0.0.nupkg --api-key <apikey> --source https://api.nuget.org/v3/index.json

```


CircleCI
```bash
dotnet nuget push nupkgs/RevitAddinMsiBuilder.*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```