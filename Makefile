MDTOOL ?= /Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool

.PHONY: all clean

all: ModernHttpClient.iOS.dll ModernHttpClient.Android.dll

package: ModernHttpClient.iOS.dll ModernHttpClient.Android.dll
	mono vendor/nuget/NuGet.exe pack ./ModernHttpClient.nuspec
	mv modernhttpclient*.nupkg ./build/

vendor:
	git submodule sync
	git submodule update --init --recursive

AFNetworking.dll: vendor
	cd vendor/afnetworking/; make

OkHttp.dll: vendor
	$(MDTOOL) build -c:Release ./vendor/okhttp/OkHttp/OkHttp.csproj
	cp ./vendor/okhttp/OkHttp/bin/Release/OkHttp.dll ./vendor/okhttp/OkHttp.dll

ModernHttpClient.Android.dll: OkHttp.dll
	$(MDTOOL) build -c:Release ./src/ModernHttpClient.Android/ModernHttpClient.Android.csproj
	mkdir -p ./build/MonoAndroid
	mv ./src/ModernHttpClient.Android/bin/Release/* ./build/MonoAndroid/

ModernHttpClient.iOS.dll: AFNetworking.dll
	$(MDTOOL) build -c:Release ./src/ModernHttpClient.iOS/ModernHttpClient.iOS.csproj
	mkdir -p ./build/MonoTouch
	mv ./src/ModernHttpClient.iOS/bin/Release/* ./build/MonoTouch/

clean:
	$(MDTOOL) build -t:Clean ModernHttpClient.sln
	rm -rf vendor
	rm -rf build
