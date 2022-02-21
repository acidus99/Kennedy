pubdir = /var/gemini/capsule/kennedy-search

publish:
	/usr/local/share/dotnet/x64/dotnet publish -c Release --self-contained -r osx-x64 -o $(pubdir) Server/Kennedy.Server.csproj
