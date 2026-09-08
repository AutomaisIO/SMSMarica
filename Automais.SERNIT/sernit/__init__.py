"""Laboratório de recon do SERNIT — SER (Sistema Estadual de Regulação) de Niterói.

Espelha o método do laboratório `Automais.SER/` (SES-RJ). Mesma plataforma
(JSF 1.2 + RichFaces/A4J 3.3.3 + Seam sobre WildFly 10), instância e build
diferentes (`2024-01-01-NITEROI`), em `regulacao.niteroi.rj.gov.br/ser`.

SOMENTE LEITURA por padrão — toda escrita passa pela trava `guardar()`.
"""
