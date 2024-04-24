import * as path from "path";
import {CfnOutput, Stage} from "aws-cdk-lib";
import {IVpc, Peer, Port, SecurityGroup} from "aws-cdk-lib/aws-ec2";
import {DockerImageAsset, NetworkMode, Platform} from "aws-cdk-lib/aws-ecr-assets";
import {Cluster, ContainerImage,} from "aws-cdk-lib/aws-ecs";
import {Effect, PolicyStatement, Role, ServicePrincipal} from "aws-cdk-lib/aws-iam";
import {Construct} from "constructs";
import {Bucket} from "aws-cdk-lib/aws-s3";
import {ApplicationLoadBalancedFargateService} from "aws-cdk-lib/aws-ecs-patterns";
import {ApplicationProtocol} from "aws-cdk-lib/aws-elasticloadbalancingv2";

export const OCR_SERVICE_PORT = 8000;

export interface OcrServiceProps {
    readonly vpc: IVpc;
    readonly ecsRole: Role;
    readonly storageBucket: Bucket
}

export class OcrServiceConstruct extends Construct {
    public readonly ecsService: ApplicationLoadBalancedFargateService;
    public readonly apiRole: Role;

    // public readonly ocrDistribution: CloudFrontWebDistribution

    constructor(scope: Construct, id: string, props: OcrServiceProps) {
        super(scope, id);
        const stageName = Stage.of(this)?.stageName || "Dev";
        const {vpc, ecsRole} = props;
        this.apiRole = new Role(this, `api-role`, {
            assumedBy: new ServicePrincipal("ec2.amazonaws.com"),
        });

        const cluster = new Cluster(this, "ApiCluster", {
            vpc,
            containerInsights: true,
        });

        this.apiRole.addToPolicy(
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

        const sg = new SecurityGroup(this, `${stageName}ocrservice`, {
            vpc: props.vpc,
            allowAllOutbound: true,
            securityGroupName: `${id}OcrServiceSecurityGroup`,
        });

        sg.addIngressRule(Peer.ipv4(vpc.vpcCidrBlock), Port.tcp(OCR_SERVICE_PORT));

        const imageAsset = new DockerImageAsset(this, `${stageName}OcrServiceImageAsset`, {
            directory:
                path.join(__dirname, "../../packages/inference-api/ekyc-inference-api"),
            platform: Platform.LINUX_AMD64,
            networkMode: NetworkMode.HOST,
        });

        this.ecsService = new ApplicationLoadBalancedFargateService(this, 'FargateService', {
            cluster,
            securityGroups: [sg],
            taskImageOptions: {
                image: ContainerImage.fromDockerImageAsset(imageAsset),
                containerPort: 8000,
                executionRole: ecsRole,
            },
            listenerPort: 8000,
            targetProtocol: ApplicationProtocol.HTTP,
            memoryLimitMiB: 16384,
            publicLoadBalancer: false,
            cpu: 4096
        });

        new CfnOutput(this, "OcrServiceDnsName", {value: this.ecsService.loadBalancer.loadBalancerDnsName});
    }
}
