#!/usr/bin/env bash
set -e

ELASTIC_URL="${ELASTIC_URL:-http://localhost:9200}"

echo "==> Aguardando ElasticSearch estar pronto em $ELASTIC_URL..."

max_retries=30
count=0
until curl -s "$ELASTIC_URL/_cat/health" > /dev/null 2>&1; do
    count=$((count + 1))
    if [ "$count" -ge "$max_retries" ]; then
        echo "ERRO: ElasticSearch não respondeu após $max_retries tentativas."
        exit 1
    fi
    echo "ElasticSearch ainda não está pronto ($count/$max_retries). Aguardando 2s..."
    sleep 2
done

echo "==> ElasticSearch pronto. Verificando existência do índice events_search_v1..."

STATUS_CODE=$(curl -s -o /dev/null -w "%{http_code}" "$ELASTIC_URL/events_search_v1")

if [ "$STATUS_CODE" -eq 200 ]; then
    echo "==> Índice events_search_v1 já existe. Pulando criação."
else
    echo "==> Criando índice events_search_v1 com analisadores pt-BR e Edge N-Gram..."
    curl -s -X PUT "$ELASTIC_URL/events_search_v1" -H 'Content-Type: application/json' -d '{
      "settings": {
        "number_of_shards": 2,
        "number_of_replicas": 1,
        "analysis": {
          "filter": {
            "pt_stemmer": {
              "type": "stemmer",
              "language": "brazilian"
            },
            "pt_stop": {
              "type": "stop",
              "stopwords": "_brazilian_"
            },
            "edge_ngram_filter": {
              "type": "edge_ngram",
              "min_gram": 2,
              "max_gram": 15
            }
          },
          "analyzer": {
            "pt_text_analyzer": {
              "type": "custom",
              "tokenizer": "standard",
              "filter": [
                "lowercase",
                "asciifolding",
                "pt_stop",
                "pt_stemmer"
              ]
            },
            "autocomplete_analyzer": {
              "type": "custom",
              "tokenizer": "standard",
              "filter": [
                "lowercase",
                "asciifolding",
                "edge_ngram_filter"
              ]
            }
          }
        }
      },
      "mappings": {
        "properties": {
          "id": { "type": "keyword" },
          "title": {
            "type": "text",
            "analyzer": "pt_text_analyzer",
            "fields": {
              "raw": { "type": "keyword" },
              "autocomplete": {
                "type": "text",
                "analyzer": "autocomplete_analyzer",
                "search_analyzer": "standard"
              }
            }
          },
          "slug": { "type": "keyword" },
          "description": {
            "type": "text",
            "analyzer": "pt_text_analyzer"
          },
          "category": { "type": "keyword" },
          "status": { "type": "keyword" },
          "age_restriction": { "type": "integer" },
          "banner_url": { "type": "keyword", "index": false },
          "schedule": {
            "properties": {
              "sales_start_at": { "type": "date" },
              "sales_end_at": { "type": "date" },
              "event_start_at": { "type": "date" },
              "event_end_at": { "type": "date" }
            }
          },
          "venue": {
            "properties": {
              "id": { "type": "keyword" },
              "name": {
                "type": "text",
                "analyzer": "pt_text_analyzer",
                "fields": { "raw": { "type": "keyword" } }
              },
              "city": { "type": "keyword" },
              "state": { "type": "keyword" },
              "country": { "type": "keyword" },
              "location": { "type": "geo_point" }
            }
          },
          "pricing": {
            "properties": {
              "min_price": { "type": "scaled_float", "scaling_factor": 100 },
              "max_price": { "type": "scaled_float", "scaling_factor": 100 },
              "currency": { "type": "keyword" }
            }
          },
          "tiers_summary": {
            "type": "nested",
            "properties": {
              "tier_id": { "type": "keyword" },
              "name": { "type": "keyword" },
              "price": { "type": "scaled_float", "scaling_factor": 100 },
              "status": { "type": "keyword" }
            }
          },
          "tags": { "type": "keyword" },
          "updated_at": { "type": "date" }
        }
      }
    }'
    echo ""

    echo "==> Configurando alias events_search apontando para events_search_v1..."
    curl -s -X POST "$ELASTIC_URL/_aliases" -H 'Content-Type: application/json' -d '{
      "actions": [
        { "add": { "index": "events_search_v1", "alias": "events_search" } }
      ]
    }'
    echo ""
    echo "==> Inicialização do ElasticSearch concluída com sucesso!"
fi
