# Changelog

All notable changes to the AWS eKYC Sample project are documented in this file.

## [Unreleased] - 2026-03-03

### Changed
- Standardized .gitignore entries across the monorepo
- Migrated package manager from Yarn to pnpm
- Updated README files with current project information
- Updated Node.js requirement from 18 to 22 LTS

### Dependencies
- Bumped axios to 1.13.5 in webv2 and ui packages
- Bumped fast-xml-parser and AWS SDK clients in PostDeploymentScripts

## [0.1.0] - 2025-07-18

### Added
- Tesseract OCR service infrastructure (ECS/Fargate) for Thai language support

### Changed
- Refactored deployment scripts for clarity
- Updated web dependencies across packages
- Migrated to Yarn 3 for node package management

### Fixed
- Improved eKYC API functionality and stability

### Dependencies
- Bumped aws-cdk-lib from 2.89.0 to 2.189.0
- Bumped SixLabors.ImageSharp
- Bumped @babel/helpers, @babel/runtime-corejs3
- Bumped http-proxy-middleware in ui package
- Bumped cookie, @aws-amplify/ui-react, @aws-amplify/ui-react-liveness, aws-amplify

## [0.0.9] - 2024-07-04

### Changed
- Removed legacy UI project (/packages/ui deprecated in favor of /packages/webv2)

### Dependencies
- Bumped micromatch in PostDeploymentScripts
- Bumped webpack from 5.76.1 to 5.94.0 in ui
- Bumped SixLabors.ImageSharp
- Bumped @adobe/css-tools in webv2
- Bumped ws from 7.5.9 to 7.5.10 in ui
- Bumped webpack-dev-middleware, express, follow-redirects in webv2
- Bumped braces in PostDeploymentScripts
- Bumped ejs from 3.1.8 to 3.1.10 in ui
- Bumped axios from 1.6.0 to 1.6.1 in ui
- Bumped express, webpack-dev-middleware, ip, follow-redirects in ui

## [0.0.8] - 2023-10-26

### Added
- Improved Thai ID field extraction

### Changed
- Removed unused environment variables and updated documentation

### Fixed
- Write path on Lambda and Fargate
- Write to /tmp in OCR service
- Lambda timeouts and filesystem writing
- IAM permissions for VPC
- CloudFront distribution allowed methods for OCR API
- Lambda paths

### Dependencies
- Bumped crypto-js from 4.1.1 to 4.2.0 in ui
- Bumped @babel/traverse to 7.23.2 in infra and ui
- Bumped postcss from 8.4.21 to 8.4.31 in ui

## [0.0.7] - 2023-09-26

### Added
- Thai ID card document support
- New web frontend (webv2) built with Cloudscape Design System and AWS Amplify UI
- Lambda functions moved into VPC

### Dependencies
- Bumped @adobe/css-tools from 4.0.1 to 4.3.1 in ui

## [0.0.6] - 2023-09-10

### Added
- webv2 package -- modern React 18 frontend with Cloudscape Design System

### Changed
- Infrastructure cleanup and reorganization

## [0.0.5] - 2023-08-02

### Changed
- Upgraded .NET runtime from 6.0 to 8.0
- Upgraded from CDK v1 to CDK v2

### Fixed
- Downgraded React Router back to v5 for code compatibility

### Dependencies
- Bumped word-wrap, semver across packages
- Bumped tough-cookie in ui and infra
- Bumped fast-xml-parser and AWS SDK clients in PostDeploymentScripts
- Bumped vm2 from 3.9.17 to 3.9.18, 3.9.16 to 3.9.17, 3.9.11 to 3.9.16 in infra
- Bumped webpack from 5.75.0 to 5.76.1 in ui

## [0.0.4] - 2023-01-09

### Changed
- Bumped project dependencies

### Dependencies
- Bumped minimatch in PostDeploymentScripts
- Bumped json5 in infra and ui
- Bumped Newtonsoft.Json in ekyc-api
- Bumped decode-uri-component in infra
- Bumped loader-utils in ui

## [0.0.3] - 2022-10-07

### Changed
- Upgraded Northstar UI framework
- Updated dependency lock files

### Dependencies
- Bumped vm2 from 3.9.9 to 3.9.11 in infra
- Bumped terser from 5.12.1 to 5.14.2 in ui
- Bumped async from 2.6.3 to 2.6.4 in ui
- Bumped ejs from 3.1.6 to 3.1.8 in ui

## [0.0.2] - 2022-04-12

### Changed
- Bumped version numbers across packages
- Refactored package.json structure
- Added colored output to deployment scripts for readability
- Added debugging information to deployment script

### Fixed
- Added missing config file preventing UI deployment
- Fixed 'Domain already associated with another user pool' error (#4)
- Fixed compilation error in post deployment scripts (#5)
- Removed incorrect reference to cfn_nag (#2)

### Dependencies
- Bumped minimist from 1.2.5 to 1.2.6 in infra and ui
- Bumped node-forge from 1.2.1 to 1.3.0 in ui
- Bumped vm2 from 3.9.5 to 3.9.6 in infra
- Bumped follow-redirects from 1.14.7 to 1.14.8 in ui

## [0.0.1] - 2021-12-21

### Added
- Initial release of the AWS eKYC sample solution
- ASP.NET Core Web API backend deployed on Lambda via API Gateway
- React frontend using Northstar design framework
- Field data extraction for Malaysian NRIC (MyKAD)
- Face extraction using Amazon Rekognition
- Liveness detection using Amazon Rekognition
- AWS CDK infrastructure-as-code (v1)
- Amazon Cognito authentication
- S3 storage for documents and training data
- DynamoDB tables for sessions and verification history
- SageMaker Ground Truth integration for model training
- EventBridge workflow orchestration
- Post-deployment scripts for Amplify configuration and SageMaker setup
- One-step deployment script
- API documentation and ML pipeline documentation
