import { useEffect, useMemo, useState } from 'react';
import { Send } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { usePermissao } from '@/shared/auth/authStore';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import { Select } from '@/shared/ui/Select';
import { useEnviarTesteModelo, useModelosWhatsApp } from '@/features/mensageria/api/queries';

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
                Variáveis ({modelo.parametros})
              </p>
              <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                {valores.map((v, i) => (
                  <label key={i} className="flex flex-col gap-1 text-sm">
                    <span className="text-gray-700">{`{{${i + 1}}}`}</span>
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
