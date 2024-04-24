#!/bin/bash
set -e

curl http://localhost:5000/swagger/v2/swagger.json -o swagger.json

rm -rf ./packages/webv2/src/apiClient

openapi-generator-cli generate -g typescript-axios -i swagger.json -o ./packages/webv2/src/apiClient  --package-name EkycApi  --type-mappings=Number=number --additional-properties=withInterfaces=true,supportsES6=true,stringEnums=false,apiPackage=ekycapi