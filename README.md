ModernHttpClient
================

# :exclamation: This project is not actively maintained. Consider using the handlers Xamarin provides. :exclamation:


This library brings the latest platform-specific networking libraries to
Xamarin applications via a custom HttpClient handler. Write your app using
System.Net.Http, but drop this library in and it will go drastically faster.
This is made possible by two native libraries:

* On iOS, [NSURLSession](https://developer.apple.com/library/ios/documentation/Foundation/Reference/NSURLSession_class/Introduction/Introduction.html)
* On Android, via [OkHttp](http://square.github.io/okhttp/)

## Usage

The good news is, you don't have to know either of these two libraries above,
using ModernHttpClient is the most boring thing in the world. Here's how
it works:

```cs
var httpClient = new HttpClient(new NativeMessageHandler());
```

## How can I use this in a PCL?

Just reference the Portable version of ModernHttpClient in your Portable
Library, and it will use the correct version on all platforms.

## Building

```sh
make
```

## Why doesn't this build in Xamarin Studio? What gives?

```sh
## Run this first
make
```
