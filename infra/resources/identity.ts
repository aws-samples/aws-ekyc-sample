import * as cognito from "aws-cdk-lib/aws-cognito";
import {CfnIdentityPool, CfnUserPoolGroup} from "aws-cdk-lib/aws-cognito";
import * as cdk from "aws-cdk-lib/core";
import * as iam from 'aws-cdk-lib/aws-iam'
import {Effect, Policy, PolicyDocument, PolicyStatement, Role, ServicePrincipal} from 'aws-cdk-lib/aws-iam'
import * as s3 from 'aws-cdk-lib/aws-s3'
import {Construct} from "constructs";
import {CfnOutput} from "aws-cdk-lib";
import {CfnWorkteam} from "aws-cdk-lib/aws-sagemaker";

interface IdentityConstructProps {
    trainingBucket: s3.Bucket
}

export class IdentityConstructs extends Construct {
    public readonly userPool: cognito.UserPool;

    public readonly userPoolClient: cognito.UserPoolClient;

    public readonly userPoolDomain: cognito.UserPoolDomain

    public readonly identityPool: CfnIdentityPool;
    public readonly labellersGroup: cognito.CfnUserPoolGroup


    public readonly groundTruthRole: iam.Role

    public readonly ecsRole: iam.Role

    public readonly workteam: CfnWorkteam


    constructor(scope: Construct, id: string, props: IdentityConstructProps) {
        super(scope, id);

        // User Pool
        this.userPool = new cognito.UserPool(this, "userpool", {
            userPoolName: "ekyc-user-pool",
            selfSignUpEnabled: false,
            signInAliases: {
                email: true,
            },
            autoVerify: {
                email: true,
            },
            standardAttributes: {
                givenName: {
                    required: true,
                    mutable: true,
                },
                familyName: {
                    required: true,
                    mutable: true,
                },
            },
            customAttributes: {
                country: new cognito.StringAttribute({mutable: true}),
                city: new cognito.StringAttribute({mutable: true}),
                isAdmin: new cognito.StringAttribute({mutable: true}),
            },
            passwordPolicy: {
                minLength: 6,
                requireLowercase: true,
                requireDigits: true,
                requireUppercase: false,
                requireSymbols: false,
            },
            accountRecovery: cognito.AccountRecovery.EMAIL_ONLY,
            removalPolicy: cdk.RemovalPolicy.DESTROY,
        });

        //  User Pool Client
        this.userPoolClient = this.userPool.addClient("userpool-client", {
            authFlows: {
                adminUserPassword: true,
                custom: true,
                userSrp: true,
                userPassword: true
            },
            generateSecret: false,
            userPoolClientName: "web-client"
        });


        this.userPoolDomain = this.userPool.addDomain(`userpool-domain-${this.node.addr}`, {
            cognitoDomain: {
                domainPrefix: `${this.node.addr}`
            }
        })

        this.identityPool = new CfnIdentityPool(this, `IdentityPool`, {
            allowUnauthenticatedIdentities: false,
            cognitoIdentityProviders: [
                {
                    clientId: this.userPoolClient.userPoolClientId,
                    providerName: this.userPool.userPoolProviderName,
                },
            ],
        });


        this.groundTruthRole = new iam.Role(this, 'GroundTruthRole', {
            assumedBy: new iam.ServicePrincipal('sagemaker.amazonaws.com'),
        })

        this.groundTruthRole.addManagedPolicy(
            iam.ManagedPolicy.fromAwsManagedPolicyName("AmazonSageMakerGroundTruthExecution")
        );

        this.groundTruthRole.addToPrincipalPolicy(
            new iam.PolicyStatement({
                resources: [
                    `*`,
                ],
                actions: [
                    "cloudwatch:PutMetricData",
                    "logs:CreateLogStream",
                    "logs:CreateLogGroup",
                    "logs:DescribeLogStreams",
                    "logs:PutLogEvents"
                ],
            })
        );

        this.groundTruthRole.addToPrincipalPolicy(
            new iam.PolicyStatement({
                resources: [
                    `arn:aws:s3:::${props.trainingBucket.bucketName}`,
                    `arn:aws:s3:::${props.trainingBucket.bucketName}/*`,
                ],
                actions: [
                    "s3:AbortMultipartUpload",
                    "s3:GetObject",
                    "s3:PutObject",
                    "s3:ListBucket",
                    "s3:GetBucketLocation"
                ],
            })
        );


        this.labellersGroup = new CfnUserPoolGroup(this, 'labellers-userpool-group', {
            userPoolId: this.userPool.userPoolId,
            groupName: 'labellers-userpool-group',
            roleArn: this.groundTruthRole.roleArn,
            description: 'Group of labellers of Ground Truth images'
        })

        // Create an inline policy document to replicate the AmazonECSTaskExecutionRolePolicy
        const ecsPolicyDoc = new PolicyDocument({
            statements: [
                // Allow ECS tasks to pull images from ECR
                new PolicyStatement({
                    effect: Effect.ALLOW,
                    actions: ["ecr:GetDownloadUrlForLayer", "ecr:BatchGetImage", "ecr:BatchCheckLayerAvailability"],
                    resources: ["*"],
                }),
                // Allow ECS tasks to send logs to CloudWatch
                new PolicyStatement({
                    effect: Effect.ALLOW,
                    actions: ["logs:CreateLogStream", "logs:PutLogEvents", "logs:CreateLogGroup"],
                    resources: ["arn:aws:logs:*:*:log-group:/ecs/*"],
                }),
                // Allow ECS tasks to describe the EC2 instances in the cluster
                new PolicyStatement({
                    effect: Effect.ALLOW,
                    actions: ["ec2:Describe*", "rekognition:*", "sagemaker:*"],
                    resources: ["*"],
                }),
                // Allow ECS tasks to access the Task Metadata Endpoint
                new PolicyStatement({
                    effect: Effect.ALLOW,
                    actions: [
                        "ecs:CreateCluster",
                        "ecs:DeregisterContainerInstance",
                        "ecs:DiscoverPollEndpoint",
                        "ecs:Poll",
                        "ecs:RegisterContainerInstance",
                        "ecs:StartTelemetrySession",
                        "ecs:Submit*",
                        "ecs:StartTask",
                        "ecs:StopTask",
                        "ecs:UpdateContainerInstancesState",
                        "ecs:UpdateService",
                    ],
                    resources: ["*"],
                }),
                // Allow ECS tasks to create and delete temporary files
                new PolicyStatement({
                    effect: Effect.ALLOW,
                    actions: [
                        "s3:CreateBucket",
                        "s3:DeleteBucket",
                        "s3:DeleteObject",
                        "s3:GetBucketLocation",
                        "s3:GetObject",
                        "s3:ListBucket",
                        "s3:PutObject",
                    ],
                    resources: ["arn:aws:s3:::codepipeline-*"],
                }),
            ],
        });

        this.ecsRole = new Role(this, "EcsExecutionRole", {
            assumedBy: new ServicePrincipal("ecs-tasks.amazonaws.com"),
        });

        const ecsRolePolicy = new Policy(this, "ecs-policy", {
            document: ecsPolicyDoc,
        });

        this.ecsRole.attachInlinePolicy(ecsRolePolicy);


        // Output

        new CfnOutput(this, "ecsRoleArn", {
            value: this.ecsRole.roleArn,
            description: "ECS Role Arn",
            exportName: `ekyc-ecsRoleArn`,
        });

        new cdk.CfnOutput(this, "UserPool", {
            value: this.userPool.userPoolId,
            description: "User pool Id",
            exportName: "userPoolId",
        });

        new cdk.CfnOutput(this, "UserPoolClient", {
            value: this.userPoolClient.userPoolClientId,
            description: "User pool client Id",
            exportName: "userPoolClientId",
        });

        new cdk.CfnOutput(this, "UserPoolDomain", {
            value: this.userPoolDomain.domainName,
            description: "User pool domain",
            exportName: "userPoolDomain",
        });

        new cdk.CfnOutput(this, "GroundTruthRoleOutput", {
            value: this.groundTruthRole.roleArn,
            description: "Ground Truth Role Arn",
            exportName: "groundTruthRoleArn",
        });

    }
}
