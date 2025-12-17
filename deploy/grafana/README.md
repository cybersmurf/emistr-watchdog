# Grafana Dashboard pro Emistr Watchdog

## Import dashboardu

### Metoda 1: Import přes UI

1. Otevřete Grafanu
2. Přejděte na **Dashboards** → **Import**
3. Klikněte na **Upload JSON file**
4. Vyberte soubor `watchdog-dashboard.json`
5. Vyberte Prometheus datasource
6. Klikněte na **Import**

### Metoda 2: Provisioning

Zkopírujte soubor do Grafana provisioning složky:

```bash
cp watchdog-dashboard.json /etc/grafana/provisioning/dashboards/
```

## Konfigurace Prometheus

Přidejte Watchdog jako scrape target do `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'watchdog'
    static_configs:
      - targets: ['watchdog-host:5050']
    metrics_path: /metrics
    scrape_interval: 30s
```

## Dostupné panely

| Panel | Popis |
|-------|-------|
| **Watchdog Uptime** | Doba běhu Watchdog služby |
| **Healthy Services** | Počet zdravých služeb |
| **Unhealthy Services** | Počet nezdravých služeb |
| **Critical Services** | Počet kritických služeb |
| **Response Time (p95)** | 95. percentil doby odezvy |
| **Health Checks (last hour)** | Počet health checků za hodinu |
| **Service Status** | Aktuální stav všech služeb |
| **Consecutive Failures** | Po sobě jdoucí selhání |
| **Notifications Sent** | Počet odeslaných notifikací |
| **Service Uptime** | Průměrná dostupnost služeb |

## Proměnné

- **datasource** - Prometheus data source
- **service** - Filtr služeb (multi-select)

## Alerting

Pro nastavení alertů použijte tyto PromQL dotazy:

### Alert na nezdravou službu
```promql
watchdog_service_status == 0
```

### Alert na kritickou službu
```promql
watchdog_service_status == -1
```

## Požadavky

- Grafana 9.0+
- Prometheus datasource
- Emistr Watchdog s povoleným `/metrics` endpointem
