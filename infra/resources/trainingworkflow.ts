import {Construct} from "constructs";
import {Duration, Names, Size, Stack} from "aws-cdk-lib";
import {Architecture, DockerImageCode, DockerImageFunction} from "aws-cdk-lib/aws-lambda"
import * as path from "path";
import * as events from "aws-cdk-lib/aws-events"
import {LambdaFunction} from "aws-cdk-lib/aws-events-targets";
import {Platform} from "aws-cdk-lib/aws-ecr-assets";
import {Bucket} from "aws-cdk-lib/aws-s3";
import {Effect, ManagedPolicy, PolicyStatement, Role, ServicePrincipal} from "aws-cdk-lib/aws-iam";
import {UserPool, UserPoolClient} from "aws-cdk-lib/aws-cognito";
import {Vpc} from "aws-cdk-lib/aws-ec2";

export interface TrainingWorkflowConstructProps {
    readonly StorageBucket: Bucket
    readonly cognitoClient: UserPoolClient
    readonly cognitoUserPool: UserPool
    readonly workteamName: string
    readonly vpc: Vpc
}

export class TrainingWorkflowConstruct extends Construct {

    labellingCompleteRule: events.Rule

    constructor(scope: Construct, id: string, props: TrainingWorkflowConstructProps) {
        super(scope, id);
        // Constructs for the labelling and training workflow
        const {vpc} = props

        const lambdaRole = new Role(this, `PostLabellingFnRole${Names.uniqueId(this)}`, {
            assumedBy: new ServicePrincipal("lambda.amazonaws.com"),
            roleName: `${id}-PostLabellingFnRole`,
        })

        lambdaRole.addManagedPolicy(
            ManagedPolicy.fromAwsManagedPolicyName(
                "service-role/AWSLambdaVPCAccessExecutionRole"
            )
        );

        // Basic execution
        lambdaRole.addToPolicy(
            new PolicyStatement({
                effect: Effect.ALLOW,
                resources: ['*'],
                actions: [
                    "logs:CreateLogGroup",
                    "logs:CreateLogStream",
                    "logs:PutLogEvents"
                ]
            })
        );

        lambdaRole.addToPolicy(
            new PolicyStatement({
                effect: Effect.ALLOW,
                resources: ["*"],
                actions: [
                    "rekognition:*"
                ]
            })
        )

        lambdaRole.addToPolicy(
            new PolicyStatement({
                effect: Effect.ALLOW,
                resources: ["*"],
                actions: [
                    "sagemaker:*"
                ]
            })
        )

        lambdaRole.addToPolicy(
            new PolicyStatement({
                effect: Effect.ALLOW,
                resources: [props.StorageBucket.bucketArn, `${props.StorageBucket.bucketArn}/*`],
                actions: [
                    "s3:PutObject",
                    "s3:GetObject",
                    "s3:DeleteObject",
                    "s3:ListBucket"
                ]
            })
        )


        const postLabellingLambda = new DockerImageFunction(this, `${id}-PostLabellingFn`, {
            code: DockerImageCode.fromImageAsset(path.join(__dirname, "./training/labelling_complete"), {
                platform: Platform.LINUX_AMD64,
                file: "dockerfile"
            }),
            vpc: vpc,
            memorySize: 3008,
            ephemeralStorageSize: Size.gibibytes(5),
            timeout: Duration.minutes(10),
            architecture: Architecture.X86_64,
            role: lambdaRole,
            environment: {
                STORAGE_BUCKET: props.StorageBucket.bucketName,
                WORKTEAM_NAME: props.workteamName
            }
        })


        props.StorageBucket.grantReadWrite(lambdaRole)


        this.labellingCompleteRule = new events.Rule(this, `${id}-labellingcomplete-evt`, {
            ruleName: `${id}-sm-labellingcomplete`,
            eventPattern: {
                source: ["aws.sagemaker"],
                account: [Stack.of(this).account],
                region: [Stack.of(this).region],
                detailType: ["SageMaker Ground Truth Labeling Job State Change"],
                detail: {LabelingJobStatus: ["Completed"]}
            }
        })

        this.labellingCompleteRule.addTarget(new LambdaFunction(postLabellingLambda, {
            maxEventAge: Duration.hours(2), // Optional: set the maxEventAge retry policy
            retryAttempts: 2, // Optional: set the max number of retry attempts
        }));

    }
}