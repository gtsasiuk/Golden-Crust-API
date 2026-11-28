# Лаб. 10 — розгортання у Kubernetes (ручні маніфести)

Образи збираються з кореня репозиторію й беруться кластером Docker Desktop локально
(`imagePullPolicy: IfNotPresent`). Секрети **не зберігаються в репозиторії** — створюються командами нижче.

## 1. Образи
```bash
docker build -t catalog-api:v1 -f Services/Catalog/Catalog.Api/Dockerfile .
docker build -t gateway:v1     -f Platform.ApiGateway/Dockerfile .
docker build -t auth-api:v1    -f Services/Auth/Auth.Api/Dockerfile .
```

## 2. Secrets (kubectl create secret, значення не потрапляють у YAML)
```bash
kubectl config use-context docker-desktop

# Паролі: SQL — ≥8 символів, великі/малі літери, цифра, символ. MQ — лише літери/цифри.
export SQL_PASSWORD='ChangeMe_Sql123!'
export MQ_PASSWORD='ChangeMeMq123'
export ADMIN_PASSWORD='Admin_Pass123!'
export JWT_KEY="$(openssl rand -base64 48)"        # ключ підпису JWT (Лаб. 09), ≥32 байтів
export CATALOG_DB="Server=sql,1433;Database=GoldenCrust_Catalog;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=True"
export IDENTITY_DB="Server=sql,1433;Database=GoldenCrust_Identity;User Id=sa;Password=${SQL_PASSWORD};TrustServerCertificate=True"

kubectl create secret generic sql-secrets       --from-literal=MSSQL_SA_PASSWORD="$SQL_PASSWORD"
kubectl create secret generic messaging-secrets --from-literal=RABBITMQ_DEFAULT_USER=goldencrust --from-literal=RABBITMQ_DEFAULT_PASS="$MQ_PASSWORD"
kubectl create secret generic catalog-secrets \
  --from-literal=ConnectionStrings__CatalogDb="$CATALOG_DB" \
  --from-literal=ConnectionStrings__messaging="amqp://goldencrust:${MQ_PASSWORD}@messaging:5672" \
  --from-literal=Jwt__SecretKey="$JWT_KEY"
kubectl create secret generic gateway-secrets   --from-literal=Jwt__SecretKey="$JWT_KEY"
kubectl create secret generic auth-secrets \
  --from-literal=ConnectionStrings__IdentityDb="$IDENTITY_DB" \
  --from-literal=Jwt__SecretKey="$JWT_KEY" \
  --from-literal=SeedAdmin__Password="$ADMIN_PASSWORD"
```
PowerShell: `$env:SQL_PASSWORD='...'`, посилання на змінні — `$env:SQL_PASSWORD`, ключ JWT —
`[Convert]::ToBase64String((1..48 | % { Get-Random -Max 256 }))`; багаторядкові команди з `\` — в один рядок.

## 3. Застосування
```bash
# спершу інфраструктура
kubectl apply -f configmap.yaml
kubectl apply -f sql-deployment.yaml -f sql-service.yaml -f redis-deployment.yaml -f redis-service.yaml -f messaging-deployment.yaml -f messaging-service.yaml
# потім сервіси (вимога лаби: gateway і catalog-api; auth-api — для отримання JWT)
kubectl apply -f catalog-api-deployment.yaml -f catalog-api-service.yaml
kubectl apply -f auth-api-deployment.yaml -f auth-api-service.yaml
kubectl apply -f gateway-deployment.yaml -f gateway-service.yaml

kubectl get pods,services
kubectl describe pod -l app=catalog-api
```
Перший запуск: catalog-api чекає SQL/RabbitMQ, тож 1–2 перезапуски подів у перші хвилини — нормально.

## 4. Перевірка Gateway → catalog-api
У проєкті немає `/api/products`: товари віддає `/api/menu-items`, і Gateway вимагає JWT.
```bash
kubectl port-forward service/gateway 8080:80
# в іншому терміналі:
curl -s -X POST http://localhost:8080/api/auth/login -H "Content-Type: application/json" \
  -d '{"userNameOrEmail":"admin","password":"Admin_Pass123!"}'          # -> accessToken
curl -s http://localhost:8080/api/menu-items -H "Authorization: Bearer <accessToken>"
kubectl logs deploy/gateway | grep catalog
```

## 5. Контрольні точки (для демонстрації)
```bash
kubectl delete pod -l app=catalog-api --wait=false && kubectl get pods -w   # Deployment створює новий под
kubectl scale deploy/sql --replicas=0                                       # база недоступна
kubectl get pods -l app=catalog-api -w     # 0/1 Running (NotReady, readiness), RESTARTS не росте
kubectl scale deploy/sql --replicas=1                                       # под знову Ready (дані emptyDir зникли — міграції пройдуть заново)
kubectl edit configmap gateway-config      # зміна видна лише після:
kubectl rollout restart deploy/gateway
kubectl get secret gateway-secrets -o yaml # значення в Base64 (не шифрування!)
```
