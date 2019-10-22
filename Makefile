MDTOOL ?= /Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool

.PHONY: all clean

all: ModernHttpClient.iOS.dll ModernHttpClient.Android.dll ModernHttpClient.Portable.dll

package: ModernHttpClient.iOS.dll ModernHttpClient.Android.dll ModernHttpClient.Portable.dll
	mono vendor/nuget/NuGet.exe pack ./ModernHttpClient.nuspec
	mv modernhttpclient*.nupkg ./build/

ModernHttpClient.Android.dll: 
	$(MDTOOL) build -c:Release ./src/ModernHttpClient.Android/ModernHttpClient.Android.csproj
	mkdir -p ./build/monoandroid
	mv ./src/ModernHttpClient.Android/bin/Release/Modern* ./build/monoandroid

ModernHttpClient.iOS.dll:
	$(MDTOOL) build -c:Release ./src/ModernHttpClient.iOS/ModernHttpClient.iOS.csproj
	mkdir -p ./build/xamarinios
	mv ./src/ModernHttpClient.iOS/bin/Release/Modern* ./build/xamarinios

ModernHttpClient.Portable.dll:
	$(MDTOOL) build -c:Release ./src/ModernHttpClient/ModernHttpClient.csproj
	mkdir -p ./build/netstandard1.6
	mv ./src/ModernHttpClient/bin/Release/Modern* ./build/netstandard1.6

clean:
	$(MDTOOL) build -t:Clean ModernHttpClient.sln
	rm *.dll
	rm -rf build
