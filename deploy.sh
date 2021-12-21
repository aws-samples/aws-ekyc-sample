#!/bin/bash
set -e
pushd infra
cdk deploy --all --require-approval never --outputs-file output.json -vv --Debug
popd
#### Run the post deployment script
pushd packages/PostDeploymentScripts
npm run start
popd
#### We need to deploy the project again so that the 
#### Amplify config is updated
pushd infra
yarn
cdk deploy --all --require-approval never --outputs-file output.json -vv --Debug
popd