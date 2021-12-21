import * as cdk from "@aws-cdk/core";
import {Construct} from "@aws-cdk/core";

import * as sagemaker from "@aws-cdk/aws-sagemaker";
import {CfnWorkteam} from "@aws-cdk/aws-sagemaker";
import * as cognito from "@aws-cdk/aws-cognito";
import {Topic} from "@aws-cdk/aws-sns";
import * as s3Deployment from "@aws-cdk/aws-s3-deployment";
import * as s3 from "@aws-cdk/aws-s3";

interface WorkteamConstructProps {
    readonly cognitoUserPool: cognito.UserPool;
    readonly cognitoAppClient: cognito.UserPoolClient;
    readonly labellersTopic: Topic;
    readonly labellersGroup: cognito.CfnUserPoolGroup;
    readonly trainingBucket: s3.Bucket
}


export default class WorkteamConstruct extends Construct {

    labellersWorkTeam: CfnWorkteam

    constructor(scope: Construct, id: string, props: WorkteamConstructProps) {
        super(scope, id);

        const strGroupName = props.labellersGroup.groupName!;

        this.labellersWorkTeam = new sagemaker.CfnWorkteam(
            this,
            "BoundingBoxWorkTeam",
            {
                description: "Work team for eKYC document bounding boxes",
                memberDefinitions: [
                    {
                        cognitoMemberDefinition: {
                            cognitoClientId: props.cognitoAppClient.userPoolClientId,
                            cognitoUserGroup: strGroupName,
                            cognitoUserPool: props.cognitoUserPool.userPoolId,
                        },
                    },
                ],
                notificationConfiguration: {
                    notificationTopicArn: props.labellersTopic.topicArn,
                },
                workteamName: "DocumentBoundingBoxWorkTeam",
            }
        )
        
       
        new cdk.CfnOutput(this, "GroundTruthWorkTeam", {
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
