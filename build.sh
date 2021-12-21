#!/bin/bash
set -e
#### Build the API project
pushd packages/ekyc-api/src/ekyc-api
dotnet publish -c Debug
popd
#### Build the Ground Truth Handler Lambda
pushd packages/lambdas/GroundTruthJobHandler/src/GroundTruthJobHandler
dotnet publish -c Debug
popd
#### Build the Check Rekogion Project Lambda
pushd packages/lambdas/CheckRekognitionProject/src/CheckRekognitionProject
dotnet publish -c Debug
popd
#### Build the User Interface
pushd packages/ui
yarn
rm -rf build
yarn run build
#### Build the CDK stack
pushd infra
yarn
rm -rf output
cdk synth --all -o output