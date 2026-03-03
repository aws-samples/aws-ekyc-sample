# eKYC Infrastructure (AWS CDK)

This CDK TypeScript project defines the AWS infrastructure for the eKYC (electronic Know Your Customer) sample application. It provisions all backend services, storage, authentication, and frontend hosting required to run the identity verification workflow.

## What it deploys

- **API Gateway + Lambda** -- ASP.NET Core 8.0 backend for document verification, liveness checks, and session management
- **S3 buckets** -- document storage, training data, web hosting, and access logs
- **DynamoDB tables** -- sessions, verification history, data requests, and training metadata
- **Amazon Cognito** -- User Pool and Identity Pool for authentication and authorization
- **CloudFront distribution** -- serves the web frontend with WAFv2 Web ACL protection
- **EventBridge rules** -- workflow orchestration for training and verification events
- **Optional ECS/Fargate service** -- Thai OCR processing (deployed when Thai language support is enabled)
- **SageMaker Ground Truth** -- integration for custom model training with labelling workteams
- **WAFv2 Web ACL** -- rate-limiting and IP-based access control for CloudFront and API Gateway
- **SNS topics** -- notifications for approval workflows
- **Systems Manager Parameter Store** -- runtime configuration shared across services

## Prerequisites

- Node.js 22+
- pnpm
- AWS CLI configured with appropriate credentials
- AWS CDK CLI (`pnpm add -g aws-cdk`)

## Commands

- `pnpm run build` -- compile TypeScript to JavaScript
- `pnpm run watch` -- watch for changes and compile automatically
- `pnpm run test` -- run Jest unit tests
- `pnpm run lint` -- run ESLint
- `cdk deploy` -- deploy the stack to your default AWS account/region
- `cdk diff` -- compare the deployed stack with the current local state
- `cdk synth` -- emit the synthesized CloudFormation template

## Key files

The `resources/` directory contains individual CDK constructs for each infrastructure component:

| File | Description |
|------|-------------|
| `api.ts` | API Gateway REST API and Lambda function |
| `storage.ts` | S3 buckets and DynamoDB tables |
| `identity.ts` | Cognito User Pool, Identity Pool, and IAM roles |
| `webapp.ts` | CloudFront distribution and web hosting |
| `events.ts` | EventBridge rules for workflow orchestration |
| `ocr-service.ts` | Optional ECS/Fargate Thai OCR service |
| `cloudfront-web-acl.ts` | WAFv2 Web ACL for CloudFront |
| `sns.ts` | SNS topics for notifications |
| `param-store.ts` | Systems Manager Parameter Store entries |
| `network.ts` | VPC and networking configuration |
| `trainingworkflow.ts` | SageMaker training workflow resources |
| `workteam.ts` | SageMaker Ground Truth labelling workteam |
