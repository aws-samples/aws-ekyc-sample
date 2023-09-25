#!/usr/bin/env node
import "source-map-support/register";
import {EkycInfraStack} from "../lib/infra-stack";
import {App} from "aws-cdk-lib";

const app = new App();

const ekycStack = new EkycInfraStack(app, "EkycInfraStack", {});