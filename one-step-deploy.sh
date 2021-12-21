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
popd
#### Build and deploy the CDK stack
pushd infra
yarn
rm -rf output
cdk synth --all -o output
cfn_nag_scan -i cdk.out/EkycInfraStack.template.json
cdk deploy --all --require-approval never --outputs-file output.json -vv --Debug
popd
#### Copy the CDK output to the right directories
cp infra/output.json packages/ekyc-api
cp infra/output.json packages/PostDeploymentScripts/src
#### Run the post deployment script
pushd packages/PostDeploymentScripts
npm run start
popd
#### We need to deploy the project again so that the 
#### Amplify config is updated
pushd infra
yarn 
rm -rf output
cdk synth --all -o output
cfn_nag_scan -i cdk.out/EkycInfraStack.template.json
cdk deploy --all --require-approval never --outputs-file output.json -vv --Debug
popd