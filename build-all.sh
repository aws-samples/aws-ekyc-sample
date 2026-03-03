#!/bin/bash
set -e

GREEN='\033[0;32m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

#### Build the .NET API
printf "${GREEN}Building eKYC API (.NET)${NC}\n"
pushd "$SCRIPT_DIR/packages/ekyc-api/src/ekyc-api"
dotnet publish -c Debug
popd

#### Build the Ground Truth Handler Lambda
printf "${GREEN}Building Ground Truth Handler Lambda (.NET)${NC}\n"
pushd "$SCRIPT_DIR/packages/lambdas/GroundTruthJobHandler/src/GroundTruthJobHandler"
dotnet publish -c Debug
popd

#### Build the Check Rekognition Project Lambda
printf "${GREEN}Building Check Rekognition Project Lambda (.NET)${NC}\n"
pushd "$SCRIPT_DIR/packages/lambdas/CheckRekognitionProject/src/CheckRekognitionProject"
dotnet publish -c Debug
popd

#### Build the web frontend
printf "${GREEN}Building Web Frontend (webv2)${NC}\n"
pushd "$SCRIPT_DIR/packages/webv2"
pnpm install --frozen-lockfile
rm -rf build
pnpm run build
popd

#### Build the post-deployment scripts
printf "${GREEN}Building Post-Deployment Scripts${NC}\n"
pushd "$SCRIPT_DIR/packages/PostDeploymentScripts"
pnpm install --frozen-lockfile
pnpm run build
popd

#### Build the CDK infra
printf "${GREEN}Building CDK Infrastructure${NC}\n"
pushd "$SCRIPT_DIR/infra"
pnpm install --frozen-lockfile
pnpm run build
rm -rf output
cdk synth --all -o output
popd

printf "${GREEN}All packages built successfully.${NC}\n"
