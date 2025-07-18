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
#### Build the Check Rekognition Project Lambda
pushd packages/lambdas/CheckRekognitionProject/src/CheckRekognitionProject
dotnet publish -c Debug
popd
#### Build the User Interface
pushd packages/webv2
yarn
rm -rf build
yarn run build
popd
#### Build the CDK stack
printf "${GREEN}Synthesizing and deploying CDK stack${NC}\n"
pushd infra
yarn
rm -rf output
cdk synth --all -o output
popd