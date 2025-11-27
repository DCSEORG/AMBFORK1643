#!/bin/bash
# deploy-with-chat.sh - Deploy Expense Management System with GenAI capabilities
# This script deploys all infrastructure including Azure OpenAI and Cognitive Search

set -e

echo "=================================================="
echo "  Expense Management System - Full Deployment"
echo "  (Including Azure OpenAI and Cognitive Search)"
echo "=================================================="
echo ""

# Configuration - Update these values
RESOURCE_GROUP="rg-expensemgmt-demo"
LOCATION="uksouth"

# Get current user info for SQL admin
echo "Getting current user information..."
ADMIN_LOGIN=$(az ad signed-in-user show --query userPrincipalName -o tsv)
ADMIN_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)

echo "Admin Login: $ADMIN_LOGIN"
echo "Admin Object ID: $ADMIN_OBJECT_ID"
echo ""

# Create resource group if it doesn't exist
echo "Step 1: Creating resource group..."
az group create --name $RESOURCE_GROUP --location $LOCATION --output none
echo "✓ Resource group created"
echo ""

# Deploy infrastructure with GenAI
echo "Step 2: Deploying Azure infrastructure (App Service, SQL Database, GenAI)..."
echo "  Note: Azure OpenAI will be deployed to Sweden Central for GPT-4o availability"
DEPLOYMENT_OUTPUT=$(az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file Infrastructure/main.bicep \
  --parameters adminObjectId=$ADMIN_OBJECT_ID adminLogin=$ADMIN_LOGIN deployGenAI=true \
  --query "properties.outputs" \
  --output json)

echo "✓ Infrastructure deployed"
echo ""

# Extract outputs
APP_SERVICE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceName.value')
APP_SERVICE_URL=$(echo $DEPLOYMENT_OUTPUT | jq -r '.appServiceUrl.value')
SQL_SERVER_FQDN=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlServerFqdn.value')
SQL_DATABASE_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.sqlDatabaseName.value')
MANAGED_IDENTITY_CLIENT_ID=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityClientId.value')
MANAGED_IDENTITY_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.managedIdentityName.value')
OPENAI_ENDPOINT=$(echo $DEPLOYMENT_OUTPUT | jq -r '.openAIEndpoint.value')
OPENAI_MODEL_NAME=$(echo $DEPLOYMENT_OUTPUT | jq -r '.openAIModelName.value')
SEARCH_ENDPOINT=$(echo $DEPLOYMENT_OUTPUT | jq -r '.searchEndpoint.value')

echo "Deployment Outputs:"
echo "  App Service: $APP_SERVICE_NAME"
echo "  App URL: $APP_SERVICE_URL"
echo "  SQL Server: $SQL_SERVER_FQDN"
echo "  Database: $SQL_DATABASE_NAME"
echo "  Managed Identity: $MANAGED_IDENTITY_NAME"
echo "  OpenAI Endpoint: $OPENAI_ENDPOINT"
echo "  OpenAI Model: $OPENAI_MODEL_NAME"
echo "  Search Endpoint: $SEARCH_ENDPOINT"
echo ""

# Configure App Service with GenAI settings
echo "Step 3: Configuring App Service with OpenAI settings..."
az webapp config appsettings set \
  --resource-group $RESOURCE_GROUP \
  --name $APP_SERVICE_NAME \
  --settings \
    "OpenAI__Endpoint=$OPENAI_ENDPOINT" \
    "OpenAI__DeploymentName=$OPENAI_MODEL_NAME" \
    "AZURE_CLIENT_ID=$MANAGED_IDENTITY_CLIENT_ID" \
    "ManagedIdentityClientId=$MANAGED_IDENTITY_CLIENT_ID" \
  --output none

echo "✓ App Service configured"
echo ""

# Wait for SQL Server to be ready
echo "Step 4: Waiting 30 seconds for SQL Server to be fully ready..."
sleep 30
echo "✓ Wait complete"
echo ""

# Add local IP to firewall
echo "Step 5: Adding local IP to SQL firewall..."
LOCAL_IP=$(curl -s https://api.ipify.org)
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server ${SQL_SERVER_FQDN%%.database.windows.net} \
  --name "LocalMachine" \
  --start-ip-address $LOCAL_IP \
  --end-ip-address $LOCAL_IP \
  --output none 2>/dev/null || echo "  (Firewall rule may already exist)"
echo "✓ Firewall configured"
echo ""

# Update Python scripts with actual values
echo "Step 6: Updating Python scripts with deployment values..."
SQL_SERVER_NAME=${SQL_SERVER_FQDN%%.database.windows.net}

# Cross-platform sed (works on Mac and Linux)
sed -i.bak "s/sql-expensemgmt-REPLACE.database.windows.net/$SQL_SERVER_FQDN/g" run-sql.py && rm -f run-sql.py.bak
sed -i.bak "s/sql-expensemgmt-REPLACE.database.windows.net/$SQL_SERVER_FQDN/g" run-sql-dbrole.py && rm -f run-sql-dbrole.py.bak
sed -i.bak "s/sql-expensemgmt-REPLACE.database.windows.net/$SQL_SERVER_FQDN/g" run-sql-stored-procs.py && rm -f run-sql-stored-procs.py.bak
sed -i.bak "s/MANAGED-IDENTITY-NAME/$MANAGED_IDENTITY_NAME/g" script.sql && rm -f script.sql.bak

echo "✓ Scripts updated"
echo ""

# Install Python dependencies
echo "Step 7: Installing Python dependencies..."
pip3 install --quiet pyodbc azure-identity
echo "✓ Dependencies installed"
echo ""

# Import database schema
echo "Step 8: Importing database schema..."
python3 run-sql.py
echo "✓ Schema imported"
echo ""

# Configure managed identity roles
echo "Step 9: Configuring managed identity database roles..."
python3 run-sql-dbrole.py
echo "✓ Identity roles configured"
echo ""

# Deploy stored procedures
echo "Step 10: Deploying stored procedures..."
python3 run-sql-stored-procs.py
echo "✓ Stored procedures deployed"
echo ""

# Build and package application
echo "Step 11: Building and packaging application..."
cd src/ExpenseManagement/ExpenseManagement
dotnet publish -c Release -o ./publish --nologo -v q

# Create zip with files at root level (not in subfolder)
cd publish
zip -r ../../../../app.zip . -x "*.pdb"
cd ../../../..

echo "✓ Application packaged"
echo ""

# Deploy application
echo "Step 12: Deploying application to Azure..."
az webapp deploy \
  --resource-group $RESOURCE_GROUP \
  --name $APP_SERVICE_NAME \
  --src-path ./app.zip \
  --type zip \
  --output none

echo "✓ Application deployed"
echo ""

echo "=================================================="
echo "  Deployment Complete!"
echo "=================================================="
echo ""
echo "Application URL: $APP_SERVICE_URL/Index"
echo ""
echo "Features enabled:"
echo "  ✓ Expense Management UI"
echo "  ✓ REST APIs with Swagger documentation"
echo "  ✓ AI-powered Chat Assistant (GPT-4o)"
echo "  ✓ Natural language expense management"
echo ""
echo "Navigate to:"
echo "  /Index   - Main expense management UI"
echo "  /Chat    - AI-powered chat assistant"
echo "  /Approve - Manager approval interface"
echo "  /swagger - API documentation"
echo ""
