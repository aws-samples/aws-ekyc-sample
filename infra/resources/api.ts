import permissionUtils from "../utils/Permissions";
import * as s3 from "aws-cdk-lib/aws-s3";
import * as dynamodb from "aws-cdk-lib/aws-dynamodb"
import * as cognito from "aws-cdk-lib/aws-cognito"
import {Topic} from "aws-cdk-lib/aws-sns";
import * as lambda from "aws-cdk-lib/aws-lambda"
import {Tracing} from "aws-cdk-lib/aws-lambda"
import * as sagemaker from "aws-cdk-lib/aws-sagemaker"
import * as apigateway from "aws-cdk-lib/aws-apigateway"
import {ApiKeySourceType, AuthorizationType} from "aws-cdk-lib/aws-apigateway"
import {StringParameter} from "aws-cdk-lib/aws-ssm";
import {ManagedPolicy, PolicyStatement, Role, ServicePrincipal} from "aws-cdk-lib/aws-iam";
import {CloudFrontWebDistribution} from "aws-cdk-lib/aws-cloudfront";
import {CfnWebACL, CfnWebACLAssociation} from "aws-cdk-lib/aws-wafv2";
import {Construct} from "constructs";
import {Arn, CfnOutput, Stack} from "aws-cdk-lib";

export interface EKYCApiConstructProps {
    readonly storageBucket: s3.Bucket;
    readonly trainingBucket: s3.Bucket;
    readonly sessionsTable: dynamodb.Table;
    readonly dataRequestsTable: dynamodb.Table;
    readonly verificationHistoryTable: dynamodb.Table;
    readonly cognitoUserPool: cognito.UserPool;
    readonly cognitoAppClient: cognito.UserPoolClient;
    readonly approvalsTopic: Topic;
    readonly RekognitionCustomLabelsProjectVersionArnParameter: StringParameter;
    readonly RekognitionCustomLabelsProjectArnParameter: StringParameter;
    readonly useFieldCoordinatesExtractionMethodParameter :StringParameter,
    readonly jsCloudFrontDistribution: CloudFrontWebDistribution;
    readonly trainingTable: dynamodb.Table;
    readonly workTeam: sagemaker.CfnWorkteam
    readonly groundTruthRole: Role
}

export default class EKYCApiConstruct extends Construct {
    public readonly execRole: Role;

    public readonly api: apigateway.LambdaRestApi;

    public webAcl: CfnWebACL;

    constructor(scope: Construct, id: string, props: EKYCApiConstructProps) {
        super(scope, id);

        const backendFn = new lambda.Function(this, "ekyc-proxy-handler", {
            runtime: lambda.Runtime.DOTNET_6,
            handler: "ekyc-api::ekyc_api.LambdaEntryPoint::FunctionHandlerAsync",
            code: lambda.Code.fromAsset(
                "../packages/ekyc-api/src/ekyc-api/bin/Debug/netcoreapp3.1"
            ),
            memorySize: 1024,
            environment: {
                StorageBucket: props.storageBucket.bucketName,
                SessionTable: props.sessionsTable.tableName,
                VerificationHistoryTable: props.verificationHistoryTable.tableName,
                AWSAccount: Stack.of(this).account,
                AWSRegion: Stack.of(this).region,
                CognitoPoolId: props.cognitoUserPool.userPoolId,
                CognitoAppClientId: props.cognitoAppClient.userPoolClientId,
                DataRequestsTable: props.dataRequestsTable.tableName,
                ApprovalsSnsTopic: props.approvalsTopic.topicArn,
                RekognitionCustomLabelsProjectVersionArnParameterName:props.RekognitionCustomLabelsProjectVersionArnParameter.parameterName,
                RekognitionCustomLabelsProjectArnParameterName:props.RekognitionCustomLabelsProjectArnParameter.parameterName,
                UseFieldCoordinatesExtractionMethodParameterName: props.useFieldCoordinatesExtractionMethodParameter.parameterName,
                OriginDistributionDomain: props.jsCloudFrontDistribution.distributionDomainName,
                TrainingBucket: props.trainingBucket.bucketName,
                TrainingTableName: props.trainingTable.tableName,
                GroundTruthRoleArn: props.groundTruthRole.roleArn,
                GroundTruthWorkTeam: `arn:aws:sagemaker:${Stack.of(this).region}:${Stack.of(this).account}:workteam/private-crowd/${props.workTeam.workteamName}`,
                GroundTruthUiTemplateS3Uri: `s3://${props.trainingBucket.bucketName}/template/labellers.html`,
            },
            tracing: Tracing.ACTIVE
        });

        const lambdaRole = backendFn.role;

        backendFn.grantInvoke(new ServicePrincipal("apigateway.amazonaws.com"));

        if (lambdaRole) {


            permissionUtils.addDynamoDbPermissions(props.sessionsTable,lambdaRole)

            permissionUtils.addDynamoDbPermissions(props.verificationHistoryTable,lambdaRole)

            permissionUtils.addDynamoDbPermissions(props.trainingTable,lambdaRole)

            permissionUtils.addDynamoDbPermissions(props.dataRequestsTable,lambdaRole)

            permissionUtils.addDynamoDbPermissions(props.dataRequestsTable,lambdaRole)

            props.storageBucket.grantReadWrite(lambdaRole);

            props.trainingBucket.grantReadWrite(lambdaRole);

            lambdaRole?.addToPrincipalPolicy(
                new PolicyStatement({
                    resources: [props.approvalsTopic.topicArn],
                    actions: ["sns:Publish"],
                })
            );

            /*lambdaRole?.addToPrincipalPolicy(
                new PolicyStatement({
                    resources: ["*"],
                    actions: ["lambda:InvokeFunction"],
                })
            );*/

            lambdaRole?.addToPrincipalPolicy(
                new PolicyStatement({
                    resources: [
                        `arn:aws:ssm:${Stack.of(this).region}:${
                            Stack.of(this).account
                        }:parameter/CFN-parametersekyc*`
                    ],
                    actions: [
                        "ssm:GetParameter",
                        "ssm:DescribeParameters",
                        "ssm:GetParameters",
                        "ssm:GetParametersByPath",
                    ],
                })
            );

            lambdaRole.addManagedPolicy(
                ManagedPolicy.fromAwsManagedPolicyName(
                    "service-role/AWSLambdaBasicExecutionRole"
                )
            );

            lambdaRole.addManagedPolicy(
                ManagedPolicy.fromAwsManagedPolicyName("AmazonSageMakerFullAccess")
            )

            lambdaRole.addManagedPolicy(
                ManagedPolicy.fromAwsManagedPolicyName("AmazonRekognitionFullAccess")
            );

            lambdaRole.addManagedPolicy(
                ManagedPolicy.fromAwsManagedPolicyName("AmazonTextractFullAccess")
            );

            lambdaRole.addManagedPolicy(
                ManagedPolicy.fromAwsManagedPolicyName("AWSXrayFullAccess")
            );
        }

        const nodeId = this.node.addr;

        const auth = new apigateway.CognitoUserPoolsAuthorizer(
            this,
            "ekycAuthorizer",
            {
                cognitoUserPools: [props.cognitoUserPool],
                identitySource: "method.request.header.Authorization",
            }
        );

        this.api = new apigateway.LambdaRestApi(this, `ekyc-data-api-${nodeId}`, {
            handler: backendFn,
            defaultCorsPreflightOptions: {
                allowOrigins: apigateway.Cors.ALL_ORIGINS,
                allowMethods: apigateway.Cors.ALL_METHODS,
                allowCredentials: true,
                allowHeaders: [
                    "Content-Type",
                    "X-Amz-Date",
                    "Authorization",
                    "X-Api-Key",
                    "X-Amz-Security-Token",
                    "X-Amz-User-Agent",
                ],
                statusCode:200
            },
            apiKeySourceType: ApiKeySourceType.HEADER,
            defaultMethodOptions: {
                apiKeyRequired: false,
                authorizer: auth,
                authorizationType: AuthorizationType.COGNITO,
            },
            endpointTypes: [apigateway.EndpointType.REGIONAL],
            proxy: true,
            deployOptions: {
                loggingLevel: apigateway.MethodLoggingLevel.INFO,
                dataTraceEnabled: true,
            },
        });

        // This workaround needed to avoid CORS preflight requests being blocked by the authorizer
        this.api.methods
            .filter((method) => method.httpMethod === "OPTIONS")
            .forEach((method) => {
                const methodCfn = method.node.defaultChild as apigateway.CfnMethod;
                methodCfn.authorizationType = apigateway.AuthorizationType.NONE;
                methodCfn.authorizerId = undefined;
                methodCfn.authorizationScopes = undefined;
                methodCfn.apiKeyRequired = false;
            });

        const deployment = new apigateway.Deployment(this, "ekyc-api-deployment", {
            api: this.api,
        });

        const stages = ["dev", "test", "uat"].map(
            (item) =>
                new apigateway.Stage(this, `${item}_stage`, {
                    deployment,
                    stageName: item,
                })
        );

        const [devStage, testStage, uatStage] = stages;

        this.api.deploymentStage = uatStage;

        new CfnOutput(this, "Data-API", {
            value: this.api.restApiId,
            description: "Data-API ID",
            exportName: "DataAPIId",
        });


        const apiArn = Arn.format(
            {
                resource: "apis",
                service: "apigateway",
                resourceName: this.api.restApiId,
            },
            Stack.of(this)
        );

        console.log(`Api ARN: ${apiArn}`);

        // Create the WAF
        this.createWAF(this.api.node.addr);

        stages.map((item) => this.addWAFtoStage(this.api, item));
    }

    private addWAFtoStage(api: apigateway.RestApi, stage: apigateway.Stage) {
        new CfnWebACLAssociation(
            this,
            `waf-assoc-${stage.node.addr}`,
            {
                webAclArn: this.webAcl.attrArn,
                resourceArn: `arn:aws:apigateway:${Stack.of(this).region}::/restapis/${
                    api.restApiId
                }/stages/${stage.stageName}`,
            }
        );
    }

    private createWAF(name: string) {
        this.webAcl = new CfnWebACL(this, `ProviderWafWebACL-${name}`, {
            name,
            description: `WebACL for ${name}`,
            defaultAction: {
                allow: {},
            },
            scope: "REGIONAL",
            tags: [
                {
                    key: "Name",
                    value: name,
                },
                {
                    key: "environment",
                    value: "prototype",
                },
            ],
            visibilityConfig: {
                cloudWatchMetricsEnabled: true,
                metricName: `waf-metric-${name}`,
                sampledRequestsEnabled: true,
            },
            rules: [
                {
                    name: "AWS-AWSManagedRulesCommonRuleSet",
                    priority: 0,
                    statement: {
                        managedRuleGroupStatement: {
                            vendorName: "AWS",
                            name: "AWSManagedRulesCommonRuleSet",
                        },
                    },
                    overrideAction: {
                        none: {},
                    },
                    visibilityConfig: {
                        sampledRequestsEnabled: true,
                        cloudWatchMetricsEnabled: true,
                        metricName: "AWS-AWSManagedRulesCommonRuleSet",
                    },
                },
                {
                    name: "AWS-AWSManagedRulesAmazonIpReputationList",
                    priority: 1,
                    statement: {
                        managedRuleGroupStatement: {
                            vendorName: "AWS",
                            name: "AWSManagedRulesAmazonIpReputationList",
                        },
                    },
                    overrideAction: {
                        none: {},
                    },
                    visibilityConfig: {
                        sampledRequestsEnabled: true,
                        cloudWatchMetricsEnabled: true,
                        metricName: "AWS-AWSManagedRulesAmazonIpReputationList",
                    },
                },
                {
                    name: "AWS-AWSManagedRulesKnownBadInputsRuleSet",
                    priority: 2,
                    statement: {
                        managedRuleGroupStatement: {
                            vendorName: "AWS",
                            name: "AWSManagedRulesKnownBadInputsRuleSet",
                        },
                    },
                    overrideAction: {
                        none: {},
                    },
                    visibilityConfig: {
                        sampledRequestsEnabled: true,
                        cloudWatchMetricsEnabled: true,
                        metricName: "AWS-AWSManagedRulesKnownBadInputsRuleSet",
                    },
                },
                {
                    name: "AWS-AWSManagedRulesLinuxRuleSet",
                    priority: 3,
                    statement: {
                        managedRuleGroupStatement: {
                            vendorName: "AWS",
                            name: "AWSManagedRulesLinuxRuleSet",
                        },
                    },
                    overrideAction: {
                        none: {},
                    },
                    visibilityConfig: {
                        sampledRequestsEnabled: true,
                        cloudWatchMetricsEnabled: true,
                        metricName: "AWS-AWSManagedRulesLinuxRuleSet",
                    },
                },
                {
                    name: "AWS-AWSManagedRulesSQLiRuleSet",
                    priority: 4,
                    statement: {
                        managedRuleGroupStatement: {
                            vendorName: "AWS",
                            name: "AWSManagedRulesSQLiRuleSet",
                        },
                    },
                    overrideAction: {
                        none: {},
                    },
                    visibilityConfig: {
                        sampledRequestsEnabled: true,
                        cloudWatchMetricsEnabled: true,
                        metricName: "AWS-AWSManagedRulesSQLiRuleSet",
                    },
                },
            ],
        });
    }
}
