import * as path from "path";
import {CfnOutput, Duration, Stack, Stage} from "aws-cdk-lib";
import {AutoScalingGroup, BlockDeviceVolume, EbsDeviceVolumeType, ScalingEvents} from "aws-cdk-lib/aws-autoscaling";
import {InstanceType, IVpc, Peer, Port, SecurityGroup} from "aws-cdk-lib/aws-ec2";
import {DockerImageAsset, NetworkMode, Platform} from "aws-cdk-lib/aws-ecr-assets";
import {
    AsgCapacityProvider,
    Cluster,
    ContainerImage,
    Ec2Service,
    Ec2TaskDefinition,
    EcsOptimizedImage,
    LogDriver,
    Protocol,
} from "aws-cdk-lib/aws-ecs";
import * as elb from "aws-cdk-lib/aws-elasticloadbalancingv2";
import {ApplicationLoadBalancer, ApplicationProtocol} from "aws-cdk-lib/aws-elasticloadbalancingv2";
import {Effect, PolicyStatement, Role, ServicePrincipal} from "aws-cdk-lib/aws-iam";
import {Topic} from "aws-cdk-lib/aws-sns";
import {Construct} from "constructs";

export const OCR_SERVICE_PORT = 8000;

export interface OcrServiceProps {
    readonly vpc: IVpc;
    readonly ecsRole: Role;
}

export class OcrServiceConstruct extends Construct {
    public readonly ecsService: Ec2Service;
    public readonly apiRole: Role;
    public readonly loadBalancer: ApplicationLoadBalancer;

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

        const asgTopic = new Topic(this, "asgTopic");

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

        const autoScalingGroup = new AutoScalingGroup(this, "OcrServiceASG", {
            vpc,
            // NOTE: this may need to be changed to a GPU instance (e.g. g4) as Pytorch is used
            //instanceType: new ec2.InstanceType('g4dn.4xlarge'),
            //machineImage: EcsOptimizedImage.amazonLinux2(AmiHardwareType.GPU),
            instanceType: new InstanceType("c5.2xlarge"),
            machineImage: EcsOptimizedImage.amazonLinux2(),
            desiredCapacity: 1,
            blockDevices: [
                {
                    deviceName: "/dev/xvda",
                    volume: BlockDeviceVolume.ebs(200, {
                        deleteOnTermination: true,
                        volumeType: EbsDeviceVolumeType.GP3,
                        encrypted: true,
                    }),
                },
            ],
            notifications: [
                {
                    topic: asgTopic,
                    scalingEvents: ScalingEvents.ALL,
                },
            ],
            securityGroup: sg,
            role: this.apiRole,
            ssmSessionPermissions: true,
        });

        const capacityProvider = new AsgCapacityProvider(this, "AsgCapacityProvider", {
            autoScalingGroup,
        });
        cluster.addAsgCapacityProvider(capacityProvider);

        const imageAsset = new DockerImageAsset(this, `${stageName}OcrServiceImageAsset`, {
            directory:
                path.join(__dirname, "../../packages/inference-api/ekyc-inference-api"),
            platform: Platform.LINUX_AMD64,
            networkMode: NetworkMode.HOST,
        });

        const taskDefinition = new Ec2TaskDefinition(this, "OcrServiceTaskDefinition", {
            taskRole: ecsRole,
        });

        taskDefinition.addContainer("OcrServiceContainer", {
            image: ContainerImage.fromDockerImageAsset(imageAsset), // replace with your image repository and tag
            memoryLimitMiB: 8192,
            cpu: 2048,
            logging: LogDriver.awsLogs({streamPrefix: "OcrService"}),
            portMappings: [
                {
                    containerPort: OCR_SERVICE_PORT,
                    hostPort: OCR_SERVICE_PORT,
                    protocol: Protocol.TCP,
                },
            ],
            environment: {
                AWS_DEFAULT_REGION: Stack.of(this).region,
            },
        });

        this.ecsService = new Ec2Service(this, "ApiService", {
            cluster: cluster,
            taskDefinition: taskDefinition,
            desiredCount: 1,
        });

        // Create a load balancer
        this.loadBalancer = new elb.ApplicationLoadBalancer(this, "lbApi", {
            vpc,
            internetFacing: false,
            securityGroup: sg,
        });

        const targetGroup = new elb.ApplicationTargetGroup(this, "tgApi", {
            targets: [this.ecsService],
            port: OCR_SERVICE_PORT, // Use the same port as specified in the Docker container
            protocol: elb.ApplicationProtocol.HTTP,
            healthCheck: {
                enabled: true,
                interval: Duration.seconds(30),
                timeout: Duration.seconds(5),
                port: OCR_SERVICE_PORT.toString(),
                protocol: elb.Protocol.HTTP,
                path: "/healthcheck",
                unhealthyThresholdCount: 5,
            },
            vpc,
        });

        // Create a listener for the load balancer
        this.loadBalancer.addListener("listenerApi", {
            port: OCR_SERVICE_PORT,
            protocol: ApplicationProtocol.HTTP,
            defaultTargetGroups: [targetGroup],
        });

        new CfnOutput(this, "OcrServiceDnsName", {value: this.loadBalancer.loadBalancerDnsName});
    }
}
