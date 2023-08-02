import * as dynamodb from "aws-cdk-lib/aws-dynamodb";
import * as iam from "aws-cdk-lib/aws-iam";

class Utils {

    static addDynamoDbPermissions = (table:dynamodb.Table,role:iam.IRole) => {

        role.addToPrincipalPolicy(
            new iam.PolicyStatement({
                resources: [
                    table.tableArn,
                ],
                actions: [
                    "dynamodb:BatchGet*",
                    "dynamodb:DescribeStream",
                    "dynamodb:DescribeTable",
                    "dynamodb:Get*",
                    "dynamodb:Query",
                    "dynamodb:Scan",
                    "dynamodb:BatchWrite*",
                    "dynamodb:CreateTable",
                    "dynamodb:Delete*",
                    "dynamodb:Update*",
                    "dynamodb:PutItem"
                ],
            })
        );

    }

}

export default Utils