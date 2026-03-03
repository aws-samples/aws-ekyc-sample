#!/bin/bash
set -e

GREEN='\033[0;32m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

#### Build all packages
"$SCRIPT_DIR/build-all.sh"

#### Deploy the CDK stack
printf "${GREEN}Deploying CDK stack${NC}\n"
pushd "$SCRIPT_DIR/infra"
cdk deploy --all --require-approval never
popd

#### Copy the CDK output to the right directories
cp "$SCRIPT_DIR/infra/output.json" "$SCRIPT_DIR/packages/ekyc-api"
cp "$SCRIPT_DIR/infra/output.json" "$SCRIPT_DIR/packages/PostDeploymentScripts/src"

#### Run the post-deployment scripts
printf "${GREEN}Running post-deployment scripts${NC}\n"
pushd "$SCRIPT_DIR/packages/PostDeploymentScripts"
pnpm run start
popd

#### Redeploy so that the Amplify config is updated
printf "${GREEN}Redeploying with updated Amplify config${NC}\n"
pushd "$SCRIPT_DIR/infra"
rm -rf output
cdk synth --all -o output
cdk deploy --all --require-approval never
popd

printf "${GREEN}Deployment complete.${NC}\n"
