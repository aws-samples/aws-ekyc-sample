import {Construct, Duration} from "@aws-cdk/core";
import * as sfn from "@aws-cdk/aws-stepfunctions";

export default class ManualApprovalWorkflowConstruct extends Construct {

    readonly approvalStateMachine: sfn.StateMachine

    constructor(scope: Construct, id: string) {
        super(scope, id)

        const pass = new sfn.Pass(this, 'ekyc-pass-step', {
            result: sfn.Result.fromObject({sessionId: 'sessionId'}),
            resultPath: '$.subObject',
        });


        // Set the next state
        const nextState = new sfn.Pass(this, 'NextState');

        const definition = pass.next(nextState)

        this.approvalStateMachine = new sfn.StateMachine(this, 'approval-state-machine', {
            definition,
            timeout: Duration.days(14),
        })
    }

}
