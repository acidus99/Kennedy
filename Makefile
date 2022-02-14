pubdir = /var/gemini/capsule/GemiSearch

publish:
	/usr/local/share/dotnet/x64/dotnet publish -c Release --self-contained -r osx-x64 -o $(pubdir)
