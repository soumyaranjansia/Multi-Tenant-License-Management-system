# Gov2Biz EKS Deployment Guide

Complete guide for deploying Gov2Biz to Amazon EKS (Elastic Kubernetes Service).

## 📋 Prerequisites

### AWS Resources Required
- AWS Account with appropriate permissions
- EKS Cluster (Kubernetes 1.28+)
- ECR (Elastic Container Registry) for Docker images
- RDS for SQL Server (Optional - or use in-cluster SQL Server)
- ElastiCache for Redis (Optional - or use in-cluster Redis)
- EFS (Elastic File System) for document storage
- ACM Certificate for HTTPS
- VPC with public and private subnets

### Local Tools Required
```bash
# AWS CLI
aws --version

# kubectl
kubectl version --client

# eksctl (for EKS cluster management)
eksctl version

# Docker
docker --version
```

## 🚀 Step-by-Step Deployment

### Step 1: Build and Push Docker Images to ECR

```bash
# Login to ECR
aws ecr get-login-password --region us-east-1 | docker login --username AWS --password-stdin <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com

# Create ECR repositories
aws ecr create-repository --repository-name gov2biz-license-service --region us-east-1
aws ecr create-repository --repository-name gov2biz-payment-service --region us-east-1
aws ecr create-repository --repository-name gov2biz-document-service --region us-east-1
aws ecr create-repository --repository-name gov2biz-notification-service --region us-east-1
aws ecr create-repository --repository-name gov2biz-api-gateway --region us-east-1
aws ecr create-repository --repository-name gov2biz-mvc-frontend --region us-east-1

# Build and push images
cd LicenseService
docker build -t <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-license-service:latest .
docker push <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-license-service:latest

cd ../PaymentService
docker build -t <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-payment-service:latest .
docker push <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-payment-service:latest

cd ../DocumentService
docker build -t <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-document-service:latest .
docker push <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-document-service:latest

cd ../NotificationService
docker build -t <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-notification-service:latest .
docker push <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-notification-service:latest

cd ../ApiGateway
docker build -t <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-api-gateway:latest .
docker push <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-api-gateway:latest

cd ../MVCFrontend
docker build -t <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-mvc-frontend:latest .
docker push <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-mvc-frontend:latest
```

### Step 2: Create EKS Cluster (if not exists)

```bash
# Create EKS cluster with eksctl
eksctl create cluster \
  --name gov2biz-cluster \
  --region us-east-1 \
  --nodegroup-name standard-workers \
  --node-type t3.xlarge \
  --nodes 3 \
  --nodes-min 2 \
  --nodes-max 10 \
  --managed

# Update kubeconfig
aws eks update-kubeconfig --region us-east-1 --name gov2biz-cluster

# Verify cluster
kubectl get nodes
```

### Step 3: Install AWS Load Balancer Controller

```bash
# Download IAM policy
curl -o iam_policy.json https://raw.githubusercontent.com/kubernetes-sigs/aws-load-balancer-controller/v2.7.0/docs/install/iam_policy.json

# Create IAM policy
aws iam create-policy \
  --policy-name AWSLoadBalancerControllerIAMPolicy \
  --policy-document file://iam_policy.json

# Create service account
eksctl create iamserviceaccount \
  --cluster=gov2biz-cluster \
  --namespace=kube-system \
  --name=aws-load-balancer-controller \
  --role-name AmazonEKSLoadBalancerControllerRole \
  --attach-policy-arn=arn:aws:iam::<ACCOUNT_ID>:policy/AWSLoadBalancerControllerIAMPolicy \
  --approve

# Install controller with Helm
helm repo add eks https://aws.github.io/eks-charts
helm repo update

helm install aws-load-balancer-controller eks/aws-load-balancer-controller \
  -n kube-system \
  --set clusterName=gov2biz-cluster \
  --set serviceAccount.create=false \
  --set serviceAccount.name=aws-load-balancer-controller

# Verify installation
kubectl get deployment -n kube-system aws-load-balancer-controller
```

### Step 4: Setup EFS for Document Storage

```bash
# Create EFS file system
aws efs create-file-system \
  --region us-east-1 \
  --performance-mode generalPurpose \
  --throughput-mode bursting \
  --tags Key=Name,Value=gov2biz-documents

# Install EFS CSI driver
kubectl apply -k "github.com/kubernetes-sigs/aws-efs-csi-driver/deploy/kubernetes/overlays/stable/?ref=release-1.7"

# Create storage class for EFS
cat <<EOF | kubectl apply -f -
apiVersion: storage.k8s.io/v1
kind: StorageClass
metadata:
  name: efs-sc
provisioner: efs.csi.aws.com
parameters:
  provisioningMode: efs-ap
  fileSystemId: <EFS_FILE_SYSTEM_ID>
  directoryPerms: "700"
EOF
```

### Step 5: Update Deployment Files

Edit `deployments-aws/deployments.yml` and replace placeholders:

```bash
# Replace ECR repository URLs
sed -i '' 's|<YOUR_ECR_REPO>|<ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com|g' deployments-aws/deployments.yml

# Update secrets in deployments-aws/deployments.yml
# - JWT_KEY
# - SQL_SA_PASSWORD
# - RAZORPAY_KEY_ID
# - RAZORPAY_KEY_SECRET
```

Edit `deployments-aws/loadbalancer-services.yml`:

```bash
# Update ACM certificate ARN
# Replace: arn:aws:acm:us-east-1:ACCOUNT_ID:certificate/CERT_ID
```

### Step 6: Deploy to EKS

```bash
# Apply deployments
kubectl apply -f deployments-aws/deployments.yml

# Wait for pods to be ready
kubectl get pods -n gov2biz -w

# Check deployment status
kubectl get deployments -n gov2biz

# Apply load balancer services
kubectl apply -f deployments-aws/loadbalancer-services.yml

# Get load balancer URLs
kubectl get svc -n gov2biz gov2biz-frontend-lb
kubectl get svc -n gov2biz gov2biz-api-gateway-lb

# Or if using Ingress
kubectl get ingress -n gov2biz
```

### Step 7: Initialize Database

```bash
# Port forward to SQL Server pod
kubectl port-forward -n gov2biz deployment/mssql 1433:1433

# In another terminal, run database initialization
docker exec -i gov2biz-mssql /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "YourStrong@Passw0rd123" -C \
  -i /scripts/Gov2Biz_Full_Database_Setup.sql

# Or copy SQL file to pod and execute
kubectl cp Scripts/Gov2Biz_Full_Database_Setup.sql gov2biz/mssql-pod-name:/tmp/init.sql
kubectl exec -n gov2biz mssql-pod-name -- /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "YourStrong@Passw0rd123" -C -i /tmp/init.sql
```

### Step 8: Configure DNS

```bash
# Get load balancer hostname
LB_HOSTNAME=$(kubectl get ingress -n gov2biz gov2biz-ingress -o jsonpath='{.status.loadBalancer.ingress[0].hostname}')

echo "Configure DNS:"
echo "www.gov2biz.com -> CNAME -> $LB_HOSTNAME"
echo "api.gov2biz.com -> CNAME -> $LB_HOSTNAME"
```

### Step 9: Verify Deployment

```bash
# Check all pods are running
kubectl get pods -n gov2biz

# Check services
kubectl get svc -n gov2biz

# Check ingress
kubectl get ingress -n gov2biz

# View logs
kubectl logs -n gov2biz deployment/license-service --tail=100

# Test health endpoints
kubectl exec -n gov2biz deployment/license-service -- curl http://localhost:8080/health
```

## 🔍 Monitoring & Troubleshooting

### View Pod Logs
```bash
kubectl logs -n gov2biz -l app=license-service --tail=50 -f
kubectl logs -n gov2biz -l app=payment-service --tail=50 -f
kubectl logs -n gov2biz -l app=mvc-frontend --tail=50 -f
```

### Describe Resources
```bash
kubectl describe deployment -n gov2biz license-service
kubectl describe pod -n gov2biz <pod-name>
kubectl describe hpa -n gov2biz
```

### Check Resource Usage
```bash
kubectl top nodes
kubectl top pods -n gov2biz
```

### Scale Deployments
```bash
# Manual scaling
kubectl scale deployment -n gov2biz license-service --replicas=5

# Check HPA status
kubectl get hpa -n gov2biz
```

## 🔒 Security Best Practices

1. **Use AWS Secrets Manager** instead of Kubernetes secrets for sensitive data
2. **Enable Pod Security Policies**
3. **Use Network Policies** (already configured)
4. **Enable AWS GuardDuty** for threat detection
5. **Use IAM Roles for Service Accounts (IRSA)**
6. **Enable CloudWatch Container Insights**
7. **Regular security scanning** of Docker images with AWS ECR scanning

## 💰 Cost Optimization

1. **Use Spot Instances** for non-critical workloads
2. **Enable Cluster Autoscaler**
3. **Right-size your pods** (adjust resource requests/limits)
4. **Use RDS for SQL Server** instead of in-cluster deployment
5. **Use ElastiCache for Redis** instead of in-cluster deployment
6. **Enable EBS gp3 volumes** instead of gp2

## 📊 Production Checklist

- [ ] ECR repositories created
- [ ] Docker images built and pushed
- [ ] EKS cluster created
- [ ] AWS Load Balancer Controller installed
- [ ] EFS file system created for documents
- [ ] Secrets updated in deployments.yml
- [ ] ACM certificate ARN updated
- [ ] Deployments applied successfully
- [ ] All pods running and healthy
- [ ] Database initialized
- [ ] Load balancers created
- [ ] DNS configured
- [ ] SSL certificate valid
- [ ] Health checks passing
- [ ] Monitoring configured
- [ ] Backup strategy in place
- [ ] Disaster recovery plan documented

## 🚨 Rollback Procedure

```bash
# Rollback to previous version
kubectl rollout undo deployment -n gov2biz license-service
kubectl rollout undo deployment -n gov2biz payment-service
kubectl rollout undo deployment -n gov2biz mvc-frontend

# Check rollout status
kubectl rollout status deployment -n gov2biz license-service

# View rollout history
kubectl rollout history deployment -n gov2biz license-service
```

## 🔄 Update Deployment

```bash
# Build new image
docker build -t <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-license-service:v1.1 .
docker push <ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-license-service:v1.1

# Update deployment
kubectl set image deployment/license-service -n gov2biz license-service=<ACCOUNT_ID>.dkr.ecr.us-east-1.amazonaws.com/gov2biz-license-service:v1.1

# Watch rollout
kubectl rollout status deployment -n gov2biz license-service
```

## 📞 Support

For issues or questions:
- Check pod logs: `kubectl logs -n gov2biz <pod-name>`
- Check events: `kubectl get events -n gov2biz --sort-by='.lastTimestamp'`
- Describe resources for detailed information

---

**Last Updated:** November 2, 2025
