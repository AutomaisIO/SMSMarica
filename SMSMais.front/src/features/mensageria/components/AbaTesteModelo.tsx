import { useEffect, useMemo, useState } from 'react';
import { AlertTriangle, Image as ImageIcon, Send } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useEnviarTesteModelo, useModelosWhatsApp } from '@/features/mensageria/api/queries';
import type { ModeloWhatsApp } from '@/features/mensageria/types';

/**
 * Enviar UM modelo para UM número, para ver no celular como a mensagem chega — texto, botões e
 * variáveis preenchidas. Não cria comunicação nem mexe em agendamento: é ensaio, não operação.
 *
 * As variáveis vêm preenchidas com o que o sistema usaria de verdade naquele modelo, então o que
 * se vê no teste é o que o paciente veria.
 */
export function AbaTesteModelo() {
  const podeEditar = usePermissao('NotificacoesAgendamento', 'Edicao');
  const modelos = useModelosWhatsApp();
  const enviar = useEnviarTesteModelo();

  const [nome, setNome] = useState('');
  const [telefone, setTelefone] = useState('');
  const [valores, setValores] = useState<string[]>([]);

  const pendentes = (modelos.data ?? []).filter((m) => m.artePendente);

  const modelo = useMemo(
    () => (modelos.data ?? []).find((m) => m.nome === nome),
    [modelos.data, nome],
  );

  // Escolheu o modelo: já preenche com os valores reais do sistema (ou os exemplos da Meta).
  useEffect(() => {
    if (!modelo) {
      setValores([]);
      return;
    }
    const base = modelo.sugestao.length > 0 ? modelo.sugestao : modelo.exemplos;
    setValores(
      Array.from({ length: modelo.parametros }, (_, i) => base[i] ?? `exemplo ${i + 1}`),
    );
  }, [modelo]);

  const telefoneOk = telefone.replace(/\D/g, '').length >= 10;
  const resultado = enviar.data;

  return (
    <div className="max-w-3xl space-y-5">
      <p className="text-sm text-gray-600">
        Manda um modelo aprovado para o número que você quiser, do jeito que o paciente receberia —
        com as variáveis preenchidas como o sistema preenche. Serve para conferir texto e botões
        antes de ligar um envio de verdade. <strong>Não</strong> cria comunicação nem altera
        agendamento.
      </p>

      {pendentes.length > 0 ? (
        <div className="flex gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-800">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <div>
            <p className="font-medium">
              {pendentes.length === 1
                ? 'Um modelo tem foto no cabeçalho e está sem arte definida.'
                : `${pendentes.length} modelos têm foto no cabeçalho e estão sem arte definida.`}
            </p>
            <p>
              A imagem vai em <strong>toda</strong> mensagem (a do modelo aprovado é só exemplo).
              Enquanto faltar, o envio é recusado antes de chegar na Meta:{' '}
              {pendentes.map((m) => m.nome).join(', ')}. A arte se define no painel do
              Automais.Zap, em Templates.
            </p>
          </div>
        </div>
      ) : null}

      {modelos.isError ? (
        <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
          Não foi possível ler os modelos aprovados: {extrairMensagemDeErro(modelos.error)}
        </p>
      ) : null}

      <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Modelo</span>
          <Select value={nome} onChange={(e) => setNome(e.target.value)} disabled={modelos.isLoading}>
            <option value="">{modelos.isLoading ? 'Carregando…' : 'Escolha o modelo'}</option>
            {(modelos.data ?? []).map((m) => (
              <option key={m.nome} value={m.nome}>
                {m.nome} ({m.categoria.toLowerCase()})
              </option>
            ))}
          </Select>
        </label>
        <label className="flex flex-col gap-1 text-sm">
          <span className="font-medium text-gray-700">Enviar para (celular com DDD)</span>
          <Input
            value={telefone}
            onChange={(e) => setTelefone(e.target.value)}
            placeholder="(21) 99999-0000"
          />
        </label>
      </div>

      {modelo ? (
        <>
          <ArteDoCabecalho modelo={modelo} />

          {modelo.corpo ? (
            <div className="rounded-lg border border-gray-200 bg-gray-50 p-3">
              <p className="mb-1 text-xs font-medium uppercase tracking-wide text-gray-500">
                Corpo aprovado na Meta
              </p>
              <p className="whitespace-pre-wrap text-sm text-gray-800">{modelo.corpo}</p>
            </div>
          ) : null}

          {modelo.parametros > 0 ? (
            <div className="space-y-2">
              <p className="text-xs font-medium uppercase tracking-wide text-gray-500">
                Variáveis ({modelo.parametros}) ·{' '}
                {modelo.nomeadas ? 'modelo com variáveis NOMEADAS' : 'modelo com variáveis numeradas'}
              </p>
              <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                {valores.map((v, i) => (
                  <label key={i} className="flex flex-col gap-1 text-sm">
                    <span className="text-gray-700">{`{{${modelo.variaveis[i] ?? i + 1}}}`}</span>
                    <Input
                      value={v}
                      onChange={(e) =>
                        setValores((atual) => atual.map((x, j) => (j === i ? e.target.value : x)))
                      }
                    />
                  </label>
                ))}
              </div>
            </div>
          ) : (
            <p className="text-sm text-gray-600">Este modelo não tem variáveis.</p>
          )}
        </>
      ) : null}

      {resultado ? (
        resultado.ok ? (
          <p className="rounded-md border border-emerald-200 bg-emerald-50 px-3 py-2 text-sm text-emerald-800">
            Enviado. Confira no celular — e lembre que a resposta cai na Central de Atendimento.
          </p>
        ) : (
          <p className="rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
            A Meta recusou: {resultado.erro}
          </p>
        )
      ) : null}
      {enviar.isError ? (
        <p className="text-sm text-red-700">{extrairMensagemDeErro(enviar.error)}</p>
      ) : null}

      {podeEditar ? (
        <Button
          disabled={!modelo || !telefoneOk || enviar.isPending}
          onClick={() => enviar.mutate({ telefone, modelo: nome, parametros: valores })}
        >
          <Send className="mr-1.5 h-4 w-4" /> Enviar teste
        </Button>
      ) : null}
    </div>
  );
}

/**
 * A arte do cabeçalho — a mesma que o envio usa.
 *
 * Só leitura: a imagem é gerida no painel do Automais.Zap, que é onde ela se publica e se
 * escolhe por modelo. Concentrar lá evita a pior versão disto — duas telas definindo a mesma
 * arte e ninguém sabendo qual está no ar.
 *
 * Modelo aprovado com foto no topo manda a imagem em CADA mensagem: a que aparece no modelo
 * aprovado na Meta é só exemplo e não vai sozinha.
 */
function ArteDoCabecalho({ modelo }: { modelo: ModeloWhatsApp }) {
  if (!modelo.cabecalhoFormato) {
    return (
      <p className="text-sm text-gray-600">
        Este modelo não tem cabeçalho — a mensagem começa direto no texto.
      </p>
    );
  }

  if (!modelo.cabecalhoExigeArte) {
    return (
      <div className="rounded-lg border border-gray-200 bg-gray-50 p-3">
        <p className="mb-1 text-xs font-medium uppercase tracking-wide text-gray-500">
          Cabeçalho de texto
        </p>
        <p className="text-sm text-gray-800">{modelo.cabecalhoTexto || '—'}</p>
      </div>
    );
  }

  return (
    <div
      className={`rounded-lg border p-3 ${
        modelo.artePendente ? 'border-red-200 bg-red-50' : 'border-gray-200 bg-gray-50'
      }`}
    >
      <p className="mb-2 flex items-center gap-1.5 text-xs font-medium uppercase tracking-wide text-gray-500">
        <ImageIcon className="h-3.5 w-3.5" />
        Arte do cabeçalho ({modelo.cabecalhoFormato.toLowerCase()})
      </p>

      {modelo.arteUrl ? (
        <div className="flex flex-wrap items-start gap-3">
          <img
            src={modelo.arteUrl}
            alt={`Arte do cabeçalho de ${modelo.nome}`}
            className="h-24 w-auto rounded border border-gray-200 bg-white object-contain"
          />
          <div className="min-w-0 flex-1 space-y-1">
            <p className="break-all text-xs text-gray-600">{modelo.arteUrl}</p>
            <p className="text-xs text-gray-500">
              {modelo.arteNaPlataforma
                ? 'Publicada e escolhida no painel do Automais.Zap — é lá que se troca.'
                : 'Padrão desta instância, válido até a plataforma definir a arte deste modelo.'}
            </p>
          </div>
        </div>
      ) : (
        <p className="flex gap-2 text-sm text-red-800">
          <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>
            Este modelo tem foto no cabeçalho e <strong>nenhuma arte definida</strong>. Enquanto
            ficar assim, o envio é recusado antes de chegar na Meta. Suba a arte no painel do
            Automais.Zap (Artes) e escolha-a em Templates.
          </span>
        </p>
      )}
    </div>
  );
}
