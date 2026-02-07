# 02-Functions: View Function Output in Azure Portal

After deployment, you can see the confirmation email log output from the function call in Azure Portal.

## Where to look
1. Open your **Function App** (for example `func-conferencehub-49152`).
2. Go to **Monitoring > Logs**.
   - If your Function App is connected to Application Insights, you can also use **Application Insights > Logs**.

## Logs Query
Run this query to find the function log lines for sender/receiver/subject/body:

```kusto
traces
| where cloud_RoleName contains "func-conferencehub"
| where message contains "Confirmation email"
   or message startswith "Sender:"
   or message startswith "Receiver:"
   or message startswith "Subject:"
   or message startswith "Body:"
| order by timestamp desc
```

## Alternative view
You can also inspect per-invocation details at:
- **Functions > SendConfirmation > Monitor**
