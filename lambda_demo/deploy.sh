#!/bin/bash

# az login
# az cloud update --endpoint-resource-manager "https://westcentralus.management.azure.com"
# az account set -s "28cbf98f-381d-4425-9ac4-cf342dab9753"
#
# code --install-extension /workspaces/bicep/lambda_demo/vscode-bicep.vsix
#
# az bicep install
# rm -f ~/.azure/bin/bicep
# ln -s /workspaces/bicep/lambda_demo/bicep ~/.azure/bin/bicep
# az group create \
#  --location westcentralus \
#  --resource-group ant-lambdademo

az deployment group create \
  --resource-group ant-lambdademo \
  --template-file /workspaces/bicep/lambda_demo/main.bicep \
  --query "properties.outputs"