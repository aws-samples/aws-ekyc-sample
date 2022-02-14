#!/bin/bash
set -e
RED='\033[0;31m'
NC='\033[0m'
#### Build the API project
printf "${RED}Building the API${NC}\n"
pushd packages/ekyc-api/src/ekyc-api
dotnet publish -c Debug
popd
#### Build the Ground Truth Handler Lambda
printf "${RED}Building Ground Truth Handler Lambda${NC}\n"
pushd packages/lambdas/GroundTruthJobHandler/src/GroundTruthJobHandler
dotnet publish -c Debug
popd
#### Build the Check Rekognition Project Lambda
printf "${RED}Building Check Rekognition Project Lambda${NC}\n"
pushd packages/lambdas/CheckRekognitionProject/src/CheckRekognitionProject
dotnet publish -c Debug
popd
#### Build the User Interface
printf "${RED}Building User Interface${NC}\n"
pushd packages/ui
yarn
rm -rf build
yarn run build
popd
#### Build and deploy the CDK stack
printf "${RED}Synthesizing and deploying CDK stack${NC}\n"
pushd infra
yarn
rm -rf output
cdk synth --all -o output
cdk deploy --all --require-approval never  --verbose --debug --outputs-file output.json > debug.json
popd
#### Copy the CDK output to the right directories
cp infra/output.json packages/ekyc-api
cp infra/output.json packages/PostDeploymentScripts/src
#### Run the post deployment script
printf "${RED}Executing post deployment scripts${NC}\n"
pushd packages/PostDeploymentScripts
yarn
npm run start
popd
#### We need to deploy the project again so that the 
#### Amplify config is updated
printf "${RED}Redeploying User Interface${NC}\n"
pushd infra
yarn 
rm -rf output
cdk synth --all -o output
cdk deploy --all --require-approval never  --verbose --debug --outputs-file output.json  > debug-2.json
popd