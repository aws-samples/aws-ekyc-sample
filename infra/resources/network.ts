import {Stage} from "aws-cdk-lib";
import {FlowLog, FlowLogDestination, FlowLogResourceType, IpAddresses, Vpc} from "aws-cdk-lib/aws-ec2";
import {Role, ServicePrincipal} from "aws-cdk-lib/aws-iam";
import {LogGroup} from "aws-cdk-lib/aws-logs";
import {Construct} from "constructs";

export class NetworkConstruct extends Construct {
  public readonly vpc: Vpc;

  constructor(scope: Construct, id: string) {
    super(scope, id);

    const stageName = Stage.of(this)?.stageName;



    const logGroup = new LogGroup(this, `${id}-vpc-logs`);

    const flowLogRole = new Role(this, `${id}-vpc-flow-log-role`, {
      assumedBy: new ServicePrincipal("vpc-flow-logs.amazonaws.com"),
    });

    logGroup.grantWrite(flowLogRole);

    this.vpc = new Vpc(this, `${stageName}-EkycVpc`, {
      maxAzs: 3, // Default is all AZs in region,
      natGateways: 1,
      ipAddresses: IpAddresses.cidr("172.31.0.0/16"),
    });

    new FlowLog(this, "FlowLog", {
      resourceType: FlowLogResourceType.fromVpc(this.vpc),
      destination: FlowLogDestination.toCloudWatchLogs(logGroup, flowLogRole),
    });
  }
}
