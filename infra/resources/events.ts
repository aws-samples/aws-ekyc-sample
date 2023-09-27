import * as lambda from "aws-cdk-lib/aws-lambda";
import * as eventtargets from 'aws-cdk-lib/aws-events-targets'
import * as events from "aws-cdk-lib/aws-events";
import * as s3 from "aws-cdk-lib/aws-s3";
import * as dynamodb from "aws-cdk-lib/aws-dynamodb";
import * as iam from 'aws-cdk-lib/aws-iam'
import {Effect, PolicyStatement} from 'aws-cdk-lib/aws-iam'
import {StringParameter} from "aws-cdk-lib/aws-ssm";
import permissionUtils from '../utils/Permissions'
import {Construct} from "constructs";

//import * as eventTargets from '@aws-cdk/aws-events-targets'

interface EventConstructsProps {
    trainingBucket: s3.Bucket;
    trainingTable: dynamodb.Table;
    readonly RekognitionCustomLabelsProjectVersionArnParameter: StringParameter;
    readonly RekognitionCustomLabelsProjectArnParameter: StringParameter;
}

export default class EventConstructs extends Construct {
    readonly triggerRekognitionCustomLabelsTraining: lambda.Function;

    constructor(scope: Construct, id: string, props: EventConstructsProps) {
        super(scope, id);

        const triggerRekognitionCustomLabelsTraining = new lambda.Function(
            this,
            "groundtruth-eventchange-handler",
            {
                runtime: lambda.Runtime.DOTNET_6,
                handler:
                    "GroundTruthJobHandler::GroundTruthJobHandler.Function::FunctionHandler",
                code: lambda.Code.fromAsset(
                    "../packages/lambdas/GroundTruthJobHandler/src/GroundTruthJobHandler/bin/Debug/net6.0"
                ),
                environment: {
                    TrainingTableName: props.trainingTable.tableName,
                    TrainingBucket: props.trainingBucket.bucketName,
                    RekognitionCustomLabelsProjectVersionArnParameterName: props.RekognitionCustomLabelsProjectVersionArnParameter.parameterName,
                    RekognitionCustomLabelsProjectArnParameterName: props.RekognitionCustomLabelsProjectArnParameter.parameterName,
                },
            }
        );

        const triggerRekognitionCustomLabelsTrainingRole =
            triggerRekognitionCustomLabelsTraining.role;

        if (triggerRekognitionCustomLabelsTrainingRole) {
            props.trainingBucket.grantReadWrite(
                triggerRekognitionCustomLabelsTrainingRole
            );

            permissionUtils.addDynamoDbPermissions(props.trainingTable, triggerRekognitionCustomLabelsTrainingRole)

            triggerRekognitionCustomLabelsTrainingRole.addManagedPolicy(
                iam.ManagedPolicy.fromAwsManagedPolicyName(
                    "service-role/AWSLambdaBasicExecutionRole"
                )
            );

            triggerRekognitionCustomLabelsTrainingRole?.addToPrincipalPolicy(new PolicyStatement({
                actions: ["ec2:DescribeNetworkInterfaces",
                    "ec2:CreateNetworkInterface",
                    "ec2:DeleteNetworkInterface",
                    "ec2:DescribeInstances",
                    "ec2:AttachNetworkInterface"],
                resources: ["*"],
                effect: Effect.ALLOW
            }))
        }

        const checkDatasetHandler = new lambda.Function(
            this,
            "check-dataset-handler",
            {
                runtime: lambda.Runtime.DOTNET_6,
                handler:
                    "CheckDatasetHandler::CheckDatasetHandler.Function::FunctionHandler",
                code: lambda.Code.fromAsset(
                    "../packages/lambdas/CheckRekognitionProject/src/CheckRekognitionProject/bin/Debug/net6.0"
                ),
                environment: {
                    TrainingTableName: props.trainingTable.tableName,
                    TrainingBucket: props.trainingBucket.bucketName,
                    RekognitionCustomLabelsProjectVersionArnParameterName: props.RekognitionCustomLabelsProjectVersionArnParameter.parameterName,
                    RekognitionCustomLabelsProjectArnParameterName: props.RekognitionCustomLabelsProjectArnParameter.parameterName,
                },
            }
        );

        const checkDatasetHandlerRole = checkDatasetHandler.role;

        if (checkDatasetHandlerRole) {

            props.trainingBucket.grantReadWrite(checkDatasetHandlerRole);

            permissionUtils.addDynamoDbPermissions(props.trainingTable, checkDatasetHandlerRole)


            checkDatasetHandlerRole.addManagedPolicy(
                iam.ManagedPolicy.fromAwsManagedPolicyName(
                    "service-role/AWSLambdaBasicExecutionRole"
                )
            );

            checkDatasetHandlerRole?.addToPrincipalPolicy(new PolicyStatement({
                actions: ["ec2:DescribeNetworkInterfaces",
                    "ec2:CreateNetworkInterface",
                    "ec2:DeleteNetworkInterface",
                    "ec2:DescribeInstances",
                    "ec2:AttachNetworkInterface"],
                resources: ["*"],
                effect: Effect.ALLOW
            }))
        }

        const GroundTruthStateChangeRule = new events.Rule(
            this,
            "ground-truth-rule",
            {
                eventPattern: {
                    source: ["aws.sagemaker"],
                    detailType: ["SageMaker Ground Truth Labeling Job State Change"],
                },
                targets: [
                    new eventtargets.LambdaFunction(
                        triggerRekognitionCustomLabelsTraining,
                        {}
                    ),
                ],
            }
        );

        const checkDatasetRule = new events.Rule(
            this,
            "CheckRekognitionDatasetStatus",
            {
                schedule: events.Schedule.cron({minute: "15"}),
                targets: [new eventtargets.LambdaFunction(checkDatasetHandler, {})],
            }
        );
    }
}
