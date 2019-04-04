MDTOOL ?= /Applications/Visual\ Studio.app/Contents/MacOS/vstool

.PHONY: all clean

all: ModernHttpClient.iOS64.dll ModernHttpClient.Android.dll ModernHttpClient.Portable.dll

package: ModernHttpClient.iOS64.dll ModernHttpClient.Android.dll ModernHttpClient.Portable.dll
	nuget pack
	mv modernhttpclient*.nupkg ./build/

ModernHttpClient.Android.dll: 
	$(MDTOOL) build -c:Release ./src/ModernHttpClient/ModernHttpClient.Android.csproj
	mkdir -p ./build/MonoAndroid
	mv ./src/ModernHttpClient/bin/Release/MonoAndroid/Modern* ./build/MonoAndroid

ModernHttpClient.iOS64.dll:
	$(MDTOOL) build -c:Release ./src/ModernHttpClient/ModernHttpClient.iOS64.csproj
	mkdir -p ./build/Xamarin.iOS10
	mv ./src/ModernHttpClient/bin/Release/Xamarin.iOS10/Modern* ./build/Xamarin.iOS10

ModernHttpClient.Portable.dll:
	$(MDTOOL) build -c:Release ./src/ModernHttpClient/ModernHttpClient.Portable.csproj
	mkdir -p ./build/Portable-Net45+WinRT45+WP8+WPA81
	mv ./src/ModernHttpClient/bin/Release/Portable-Net45+WinRT45+WP8+WPA81/Modern* ./build/Portable-Net45+WinRT45+WP8+WPA81

clean:
	$(MDTOOL) build -t:Clean ModernHttpClient.sln
	rm *.dll
	rm -rf build
