dotnet tool uninstall -g PrettyStick.TUI
dotnet tool uninstall -g Bender.TUI
dotnet pack -c Release 
dotnet tool install -g Bender.TUI --source nupkg