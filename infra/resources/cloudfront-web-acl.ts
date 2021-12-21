// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: MIT-0
 import {
    AwsCustomResource,
    AwsCustomResourcePolicy,
    PhysicalResourceId,
    PhysicalResourceIdReference,
  } from '@aws-cdk/custom-resources';
  import { Construct } from '@aws-cdk/core';
  import { WAFV2 } from 'aws-sdk';
  ​
  export interface CloudFrontWebAclProps {
    readonly name: string;
    readonly managedRules: WAFV2.ManagedRuleGroupStatement[];
  }
  ​
  /**
   * This construct creates a WAFv2 Web ACL for cloudfront in the us-east-1 region (required for cloudfront) no matter the
   * region of the parent cloudformation/cdk stack.
   */
  export default class CloudFrontWebAcl extends Construct {
    public readonly webAclId: string;
    public readonly name: string;
    public readonly region: string = 'us-east-1';
  ​
    constructor(scope: Construct, id: string, props: CloudFrontWebAclProps) {
      super(scope, id);
  ​
      this.name = props.name;
      const Scope = 'CLOUDFRONT';
  ​
      // The parameters for creating the Web ACL
      const createWebACLRequest: WAFV2.Types.CreateWebACLRequest = {
        Name: this.name,
        DefaultAction: { Allow: {} },
        Scope,
        VisibilityConfig: {
          CloudWatchMetricsEnabled: true,
          MetricName: id,
          SampledRequestsEnabled: true,
        },
        Rules: props.managedRules.map((rule, Priority) => ({
          Name: `${rule.VendorName}-${rule.Name}`,
          Priority,
          Statement: { ManagedRuleGroupStatement: rule },
          OverrideAction: { None: {} },
          VisibilityConfig: {
            MetricName: `${rule.VendorName}-${rule.Name}`,
            CloudWatchMetricsEnabled: true,
            SampledRequestsEnabled: true,
          },
        })),
      };
  ​
      // Create the Web ACL
      const createCustomResource = new AwsCustomResource(this, `${id}-Create`, {
        policy: AwsCustomResourcePolicy.fromSdkCalls({
          resources: AwsCustomResourcePolicy.ANY_RESOURCE,
        }),
        onCreate: {
          service: 'WAFV2',
          action: 'createWebACL',
          parameters: createWebACLRequest,
          region: this.region,
          physicalResourceId: PhysicalResourceId.fromResponse('Summary.Id'),
        },
      });
      this.webAclId = createCustomResource.getResponseField('Summary.Id');
  ​
      const getWebACLRequest: WAFV2.Types.GetWebACLRequest = {
        Name: this.name,
        Scope,
        Id: this.webAclId,
      };
  ​
      // A second custom resource is used for managing the deletion of this construct, since both an Id and LockToken
      // are required for Web ACL Deletion
      new AwsCustomResource(this, `${id}-Delete`, {
        policy: AwsCustomResourcePolicy.fromSdkCalls({
          resources: AwsCustomResourcePolicy.ANY_RESOURCE,
        }),
        onCreate: {
          service: 'WAFV2',
          action: 'getWebACL',
          parameters: getWebACLRequest,
          region: this.region,
          physicalResourceId: PhysicalResourceId.fromResponse('LockToken'),
        },
        onDelete: {
          service: 'WAFV2',
          action: 'deleteWebACL',
          parameters: {
            Name: this.name,
            Scope,
            Id: this.webAclId,
            LockToken: new PhysicalResourceIdReference(),
          },
          region: this.region,
        },
      });
    }
  ​
    public getArn = (account: string) =>
      `arn:aws:wafv2:${this.region}:${account}:global/webacl/${this.name}/${this.webAclId}`;
  }