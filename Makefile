MDTOOL ?= /Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool

.PHONY: all clean

all: ModernHttpClient.iOS.dll ModernHttpClient.Android.dll

package: ModernHttpClient.iOS.dll ModernHttpClient.Android.dll
	mono vendor/nuget/NuGet.exe pack ./ModernHttpClient.nuspec
	mv modernhttpclient*.nupkg ./build/

submodule:
	git submodule sync
	git submodule update --init --recursive

OkHttp.dll: submodule
	$(MDTOOL) build -c:Release ./vendor/okhttp/OkHttp/OkHttp.csproj
	cp ./vendor/okhttp/OkHttp/bin/Release/OkHttp.dll ./vendor/okhttp/OkHttp.dll

ModernHttpClient.Android.dll: OkHttp.dll
	$(MDTOOL) build -c:Release ./src/ModernHttpClient/ModernHttpClient.Android.csproj
	mkdir -p ./build/MonoAndroid
	mv ./src/ModernHttpClient/bin/Release/MonoAndroid/* ./build/MonoAndroid

ModernHttpClient.iOS.dll:
	$(MDTOOL) build -c:Release ./src/ModernHttpClient/ModernHttpClient.iOS.csproj
	mkdir -p ./build/MonoTouch
	mv ./src/ModernHttpClient/bin/Release/MonoTouch/* ./build/MonoTouch

clean:
	$(MDTOOL) build -t:Clean ModernHttpClient.sln
	rm *.dll
	rm -rf build
