import * as cognito from "@aws-cdk/aws-cognito";
import {CfnUserPoolGroup, OAuthScope, UserPoolClientIdentityProvider, UserPoolDomain} from "@aws-cdk/aws-cognito";
import * as cdk from "@aws-cdk/core";
import * as iam from '@aws-cdk/aws-iam'
import * as s3 from '@aws-cdk/aws-s3'

import * as cloudfront from "@aws-cdk/aws-cloudfront";

interface IdentityConstructProps {
    cfJsWebApp: cloudfront.CloudFrontWebDistribution
    trainingBucket: s3.Bucket
}

export class IdentityConstructs extends cdk.Construct {
    public readonly userPool: cognito.UserPool;

    public readonly userPoolClient: cognito.UserPoolClient;

    public readonly userPoolDomain: cognito.UserPoolDomain

    public readonly identityPool: cognito.CfnIdentityPool

    public readonly labellersGroup: cognito.CfnUserPoolGroup

    public readonly labellersClient: cognito.UserPoolClient

    public readonly groundTruthRole: iam.Role


    constructor(scope: cdk.Construct, id: string, props: IdentityConstructProps) {
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

        const standardCognitoAttributes = {
            givenName: true,
            familyName: true,
            email: true,
            emailVerified: true,
            address: true,
            birthdate: true,
            gender: true,
            locale: true,
            middleName: true,
            fullname: true,
            nickname: true,
            phoneNumber: true,
            phoneNumberVerified: true,
            profilePicture: true,
            preferredUsername: true,
            profilePage: true,
            timezone: true,
            lastUpdateTime: true,
            website: true,
        };

        const clientReadAttributes = new cognito.ClientAttributes()
            .withStandardAttributes(standardCognitoAttributes)
            .withCustomAttributes(...["country", "city", "isAdmin"]);

        const clientWriteAttributes = new cognito.ClientAttributes()
            .withStandardAttributes({
                ...standardCognitoAttributes,
                emailVerified: false,
                phoneNumberVerified: false,
            })
            .withCustomAttributes(...["country", "city"]);

        //  User Pool Client
        this.userPoolClient = new cognito.UserPoolClient(this, "userpool-client", {
            userPool: this.userPool,
            authFlows: {
                adminUserPassword: true,
                custom: true,
                userSrp: true,
                userPassword: true
            },
            disableOAuth: false,
            readAttributes: clientReadAttributes,
            writeAttributes: clientWriteAttributes,
            generateSecret: false,
            oAuth: {
                flows: {authorizationCodeGrant: true, implicitCodeGrant: true, clientCredentials: false},
                scopes: [OAuthScope.OPENID, OAuthScope.EMAIL, OAuthScope.COGNITO_ADMIN],
                callbackUrls: [`https://${props.cfJsWebApp.distributionDomainName}`]
            },
            supportedIdentityProviders: [
                UserPoolClientIdentityProvider.COGNITO,
            ]
        });

        this.userPoolDomain = new UserPoolDomain(this, 'userpool-domain', {
            userPool: this.userPool,
            cognitoDomain: {
                domainPrefix: `ekyc-prototype-${this.node.addr}`
            }

        })

        this.labellersClient = new cognito.UserPoolClient(this, "labellers-client", {
            userPool: this.userPool,
            authFlows: {
                adminUserPassword: true,
                custom: true,
                userSrp: true,
            },
            disableOAuth: false,
            readAttributes: clientReadAttributes,
            writeAttributes: clientWriteAttributes,
            generateSecret: true,
            supportedIdentityProviders: [
                UserPoolClientIdentityProvider.COGNITO,
            ],
            oAuth: {
                flows: {authorizationCodeGrant: true, implicitCodeGrant: true, clientCredentials: false},
                scopes: [OAuthScope.OPENID, OAuthScope.EMAIL, OAuthScope.COGNITO_ADMIN],
                callbackUrls: [`https://${props.cfJsWebApp.distributionDomainName}`]
            },
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


        // Output

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

        new cdk.CfnOutput(this, "LabellersPoolClientId", {
            value: this.labellersClient.userPoolClientId,
            description: "Labellers pool client Id",
            exportName: "labellersPoolClientId",
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
