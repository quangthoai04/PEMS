# PEMS

## Architecture Guard
To prevent legacy root folders from being accidentally reintroduced during merges or branch switches, a guard script has been created.
**Before committing or merging code**, please run the following script to verify the project structure:

```powershell
.\scripts\guard-project-structure.ps1
```

If the script fails, it means old root folders (`Application/`, `Domain/`, `Infrastructure/`, `Pems_WebAPI/`, `Scaffold/`) have reappeared. You must delete them before proceeding.
