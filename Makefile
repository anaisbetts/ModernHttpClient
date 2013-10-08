MDTOOL=/Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool

all: ModernHttpClient.iOS.dll ModernHttpClient.Android.dll

vendor:
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
	rm -r vendor
	rm -r build