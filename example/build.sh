#!/bin/bash

#install zip
apt-get -qq update
apt-get -qq -y install zip

dotnet restore

#create deployment package
dotnet lambda package --configuration debug --framework netcoreapp2.1 --output-package bin/debug/netcoreapp2.1/deploy-package.zip
