# 'make component' will build 'all' and then create the Xamarin component.

MDTOOL ?= /Applications/Xamarin\ Studio.app/Contents/MacOS/mdtool

all: vendor
	$(MAKE) -w -C ..

vendor: ../vendor

../vendor:
	$(MAKE) -w -C .. vendor 

clean:
	$(MAKE) -w -C .. clean
	$(MDTOOL) build -t:Clean -c:Release ../samples/HttpClient.iOS/HttpClient.iOS.sln
	rm -rf ../samples/HttpClient.iOS/bin ../samples/HttpClient.iOS/obj
	$(MDTOOL) build -t:Clean -c:Release ../samples/HttpClient.Android/HttpClient.Android.sln
	rm -rf ../samples/HttpClient.Android/bin ../samples/HttpClient.Android/obj
	rm -f $(xamfile)

# Component vars and targets

.PHONY: all xam component install uninstall clean vendor patch

# Path to xamarin-component.exe (get it from https://components.xamarin.com/guidelines)
XAMARIN_COMPONENT ?= ./xamarin-component.exe

# Path to mono, which by default should be on your PATH.
MONO ?= mono

getval = grep ^$(1): component.yaml | sed 's/^[^:]*: *"\{0,1\}\([^"]\{1,\}\)"\{0,1\}/\1/'

NAME := $(shell $(call getval,name))
VERSION ?= $(shell $(call getval,version))

pkgid := $(shell $(call getval,id))
xamfile := $(pkgid)-$(VERSION).xam

# This is Mac-specific:
appdatadir := $(HOME)/Library/Caches/Xamarin

# These are all synonyms
xam: $(xamfile)
package: $(xamfile)
component: $(xamfile)

# The 'package' directive to xamarin-component will cause it to find this same Makefile and run the default target.
$(xamfile):
	$(MONO) $(XAMARIN_COMPONENT) package

# Convenience target that will install the component in your local XS installation.
install: package
	$(MONO) $(XAMARIN_COMPONENT) install $(xamfile)

# While testing you may need to remove the component from XS with this.
uninstall:
	@if [ ! -z "`ps xc | grep XamarinStudio`" ]; then \
		echo Modules cannot be uninstalled with XamarinStudio running.; \
		exit 1; \
	fi
	rm -f $(appdatadir)/ComponentInfo/$(pkgid)-* $(appdatadir)/Components/$(pkgid)-*
	@echo $$'\nRemember to remove the Components directory from projects that use this component.'
