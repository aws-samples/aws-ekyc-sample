import * as sagemaker from "aws-cdk-lib/aws-sagemaker";
import {CfnWorkteam} from "aws-cdk-lib/aws-sagemaker";
import * as cognito from "aws-cdk-lib/aws-cognito";
import {CfnUserPoolGroup} from "aws-cdk-lib/aws-cognito";
import {Topic} from "aws-cdk-lib/aws-sns";
import * as s3Deployment from "aws-cdk-lib/aws-s3-deployment";
import * as s3 from "aws-cdk-lib/aws-s3";
import {Construct} from "constructs";
import {CfnOutput} from "aws-cdk-lib";
import * as cdk from "aws-cdk-lib/core";

export interface WorkteamConstructProps {

    readonly labellersTopic: Topic;
    readonly trainingBucket: s3.Bucket
}

export default class WorkteamConstruct extends Construct {

    labellersWorkTeam: CfnWorkteam
    labellersUserPool: cognito.UserPool
    labellersUserPoolClient: cognito.UserPoolClient

    constructor(scope: Construct, id: string, props: WorkteamConstructProps) {
        super(scope, id);

        this.labellersUserPool = new cognito.UserPool(this, "labellers-userpool", {
            userPoolName: "labellers-user-pool",
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
        this.labellersUserPoolClient = this.labellersUserPool.addClient("labellersuserpool-client", {
            authFlows: {
                adminUserPassword: true,
                custom: true,
                userSrp: true,
                userPassword: true
            },
            generateSecret: true,
            userPoolClientName: "labelling-client"
        });


        const labellersUserPoolDomain = this.labellersUserPool.addDomain(`userpool-domain-${this.node.addr}`, {
            cognitoDomain: {
                domainPrefix: `${this.node.addr}`
            }
        })

        const userPoolGroup = new CfnUserPoolGroup(this, "labelUserGroup", {
            groupName: "labellers",
            userPoolId: this.labellersUserPool.userPoolId
        })

        const strGroupName = userPoolGroup.groupName!;

        this.labellersWorkTeam = new sagemaker.CfnWorkteam(
            this,
            "BoundingBoxWorkTeam",
            {
                description: "Work team for eKYC document bounding boxes",
                memberDefinitions: [
                    {
                        cognitoMemberDefinition: {
                            cognitoClientId: this.labellersUserPoolClient.userPoolClientId,
                            cognitoUserGroup: strGroupName,
                            cognitoUserPool: this.labellersUserPool.userPoolId,
                        },
                    },
                ],
                notificationConfiguration: {
                    notificationTopicArn: props.labellersTopic.topicArn,
                },
                workteamName: "DocumentBoundingBoxWorkTeam",
            }
        )


        new CfnOutput(this, "GroundTruthWorkTeam", {
            value: this.labellersWorkTeam.workteamName!,
            description: "Name of work team for Ground Truth",
            exportName: "groundTruthWorkTeam",
        });

        const deployment = new s3Deployment.BucketDeployment(
            this,
            "deployGroundTruthTemplate",
            {
                sources: [s3Deployment.Source.asset("resources/ground-truth-template")],
                destinationBucket: props.trainingBucket,
                destinationKeyPrefix: "template",
            }
        )
    }
}
