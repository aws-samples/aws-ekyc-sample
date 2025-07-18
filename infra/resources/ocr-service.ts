import * as path from "path";
import {CfnOutput, Stage} from "aws-cdk-lib";
import {IVpc, Peer, Port, SecurityGroup} from "aws-cdk-lib/aws-ec2";
import {DockerImageAsset, Platform} from "aws-cdk-lib/aws-ecr-assets";
import {
    Cluster,
    ContainerImage,
    CpuArchitecture,
    FargateTaskDefinition,
    LogDriver,
    OperatingSystemFamily,
} from "aws-cdk-lib/aws-ecs";
import {Effect, PolicyStatement, Role} from "aws-cdk-lib/aws-iam";
import {Construct} from "constructs";
import {Bucket} from "aws-cdk-lib/aws-s3";
import {ApplicationLoadBalancedFargateService} from "aws-cdk-lib/aws-ecs-patterns";
import {ApplicationProtocol} from "aws-cdk-lib/aws-elasticloadbalancingv2";
import {SociIndexBuild} from "deploy-time-build";
import {LogGroup, RetentionDays} from "aws-cdk-lib/aws-logs";
import {RemovalPolicy} from "aws-cdk-lib/core";

export const OCR_SERVICE_PORT = 8000;

export interface OcrServiceProps {
    readonly vpc: IVpc;
    readonly ecsRole: Role;
    readonly storageBucket: Bucket
}

export class OcrServiceConstruct extends Construct {
    public readonly ecsService: ApplicationLoadBalancedFargateService;


    // public readonly ocrDistribution: CloudFrontWebDistribution

    constructor(scope: Construct, id: string, props: OcrServiceProps) {
        super(scope, id);
        const stageName = Stage.of(this)?.stageName || "Dev";
        const {vpc, ecsRole} = props;


        const cluster = new Cluster(this, "ApiCluster", {
            vpc,
            containerInsights: true,
        });


        const sg = new SecurityGroup(this, `${stageName}ocrservice`, {
            vpc: props.vpc,
            allowAllOutbound: true,
            securityGroupName: `${id}OcrServiceSecurityGroup`,
        });

        sg.addIngressRule(Peer.ipv4(vpc.vpcCidrBlock), Port.tcp(OCR_SERVICE_PORT));

        const taskDefinition = new FargateTaskDefinition(
            this,
            "TaskDefinition",
            {
                cpu: 4096,
                memoryLimitMiB: 16384,
                runtimePlatform: {
                    cpuArchitecture: CpuArchitecture.ARM64,
                    operatingSystemFamily: OperatingSystemFamily.LINUX,
                },
            }
        );

        taskDefinition.addToTaskRolePolicy(
            new PolicyStatement({
                effect: Effect.ALLOW,
                resources: ["*"],
                actions: [
                    "ecs:CreateCluster",
                    "ecs:DeregisterContainerInstance",
                    "ecs:DiscoverPollEndpoint",
                    "ecs:Poll",
                    "ecs:RegisterContainerInstance",
                    "ecs:StartTelemetrySession",
                    "ecs:Submit*",
                    "ssm:GetParameters",
                    "ecr:GetAuthorizationToken",
                    "ecr:BatchCheckLayerAvailability",
                    "ecr:GetDownloadUrlForLayer",
                    "ecr:BatchGetImage",
                    "logs:CreateLogStream",
                    "logs:PutLogEvents",
                    "ec2:AuthorizeSecurityGroupIngress",
                    "ec2:Describe*",
                    "elasticloadbalancing:DeregisterInstancesFromLoadBalancer",
                    "elasticloadbalancing:DeregisterTargets",
                    "elasticloadbalancing:Describe*",
                    "elasticloadbalancing:RegisterInstancesWithLoadBalancer",
                    "elasticloadbalancing:RegisterTargets",
                    "textract:DetectDocumentText",
                ],
            }),
        );


        const taskLogGroup = new LogGroup(this, "OcrServiceLogGroup", {
            removalPolicy: RemovalPolicy.DESTROY,
            retention: RetentionDays.ONE_WEEK,
        });

        const asset = new DockerImageAsset(this, "Image", {
            directory: path.join(__dirname, "../../packages/inference-api/ekyc-inference-api"),
            platform: Platform.LINUX_ARM64,
        });
        SociIndexBuild.fromDockerImageAsset(this, "Index", asset);

        const container = taskDefinition.addContainer('Container', {
            image: ContainerImage.fromDockerImageAsset(asset),
            logging: LogDriver.awsLogs({
                streamPrefix: "ocr-service",
                logGroup: taskLogGroup,
            }),
        })

        container.addPortMappings({
            containerPort: 8000,
            hostPort: 8000
        })

        this.ecsService = new ApplicationLoadBalancedFargateService(this, 'FargateService', {
            cluster,
            securityGroups: [sg],
            taskDefinition,
            listenerPort: 8000,
            targetProtocol: ApplicationProtocol.HTTP,
            memoryLimitMiB: 16384,
            publicLoadBalancer: false,
            cpu: 4096
        });

        new CfnOutput(this, "OcrServiceDnsName", {value: this.ecsService.loadBalancer.loadBalancerDnsName});
    }
}
