import { useEffect, useState } from 'react';
import { BellRing, Loader2, Plus, Send, X } from 'lucide-react';
import { extrairMensagemDeErro } from '@/shared/api/httpClient';
import { Button } from '@/shared/ui/Button';
import { Input } from '@/shared/ui/Input';
import {
  useSalvarTelefonesNotificacao,
  useTelefonesNotificacao,
  useTestarNotificacaoSincronismo,
} from '@/features/sisreg/api/queries';

type Props = {
  /** `sisreg`, `ser` ou `sernit`. */
  provedor: string;
  /** Nome do sistema como o operador o chama. */
  rotulo: string;
  podeEditar: boolean;
};

/** "21979997000" → "(21) 97999-7000". Só para exibir; o backend guarda os dígitos. */
function formatar(telefone: string): string {
  const d = telefone.replace(/\D/g, '');
  const local = d.length > 11 ? d.slice(-11) : d; // 55 na frente não entra na máscara
  if (local.length === 11) return `(${local.slice(0, 2)}) ${local.slice(2, 7)}-${local.slice(7)}`;
  if (local.length === 10) return `(${local.slice(0, 2)}) ${local.slice(2, 6)}-${local.slice(6)}`;
  return telefone;
}

/**
 * Quem recebe aviso por WhatsApp quando este sincronismo falha ou precisa de gente.
 *
 * <p>Os motores rodam de madrugada e sozinhos: quando o SISREG pede CAPTCHA, ele para e ninguém
 * fica sabendo até alguém abrir a tela — o que na prática é descobrir depois que o paciente já
 * perdeu a consulta.</p>
 */
export function TelefonesNotificacaoSecao({ provedor, rotulo, podeEditar }: Props) {
  const consulta = useTelefonesNotificacao(provedor);
  const salvar = useSalvarTelefonesNotificacao(provedor);
  const testar = useTestarNotificacaoSincronismo(provedor);

  const [telefones, setTelefones] = useState<string[]>([]);
  const [novo, setNovo] = useState('');
  const [aviso, setAviso] = useState<{ tipo: 'ok' | 'erro'; texto: string } | null>(null);

  useEffect(() => {
    if (consulta.data) setTelefones(consulta.data);
  }, [consulta.data]);

  async function aplicar(lista: string[], sucesso: string) {
    setAviso(null);
    try {
      const salvos = await salvar.mutateAsync(lista);
      setTelefones(salvos);
      setAviso({ tipo: 'ok', texto: sucesso });
    } catch (e) {
      setAviso({ tipo: 'erro', texto: extrairMensagemDeErro(e) });
    }
  }

  function adicionar() {
    const digitos = novo.replace(/\D/g, '');
    if (digitos.length < 10) {
      setAviso({ tipo: 'erro', texto: 'Informe o telefone com DDD.' });
      return;
    }
    if (telefones.includes(digitos)) {
      setNovo('');
      return;
    }
    setNovo('');
    void aplicar([...telefones, digitos], 'Telefone adicionado.');
  }

  return (
    <section className="rounded-xl border border-gray-200 bg-white p-5 shadow-sm">
      <header className="mb-3">
        <h2 className="flex items-center gap-2 text-lg font-semibold text-gray-900">
          <BellRing className="h-5 w-5 text-primary-600" />
          Avisos de falha do sincronismo {rotulo}
        </h2>
        <p className="mt-1 text-sm text-gray-600">
          Quem recebe no WhatsApp quando o sincronismo falha ou precisa de alguém — CAPTCHA,
          credencial derrubada, unidade com erro. O sincronismo roda de madrugada e sozinho; sem
          aviso, uma parada só aparece quando alguém abre esta tela.
        </p>
      </header>

      {aviso && (
        <p
          className={`mb-3 rounded-md border px-3 py-2 text-sm ${
            aviso.tipo === 'ok'
              ? 'border-emerald-200 bg-emerald-50 text-emerald-800'
              : 'border-red-200 bg-red-50 text-red-700'
          }`}
        >
          {aviso.texto}
        </p>
      )}

      {consulta.isLoading ? (
        <p className="text-sm text-gray-500">Carregando…</p>
      ) : (
        <>
          {telefones.length === 0 ? (
            <p className="mb-3 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-800">
              Ninguém está sendo avisado. Uma falha de madrugada passará despercebida até alguém
              olhar a tela.
            </p>
          ) : (
            <ul className="mb-3 flex flex-wrap gap-2">
              {telefones.map((t) => (
                <li
                  key={t}
                  className="flex items-center gap-2 rounded-full border border-gray-200 bg-gray-50 px-3 py-1 text-sm text-gray-800"
                >
                  {formatar(t)}
                  {podeEditar && (
                    <button
                      type="button"
                      title="Remover"
                      className="text-gray-400 hover:text-red-600"
                      onClick={() =>
                        aplicar(
                          telefones.filter((x) => x !== t),
                          'Telefone removido.',
                        )
                      }
                    >
                      <X className="h-3.5 w-3.5" />
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}

          {podeEditar && (
            <div className="flex flex-wrap items-center gap-2">
              <Input
                className="w-52"
                placeholder="(21) 99999-0000"
                value={novo}
                onChange={(e) => setNovo(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === 'Enter') {
                    e.preventDefault();
                    adicionar();
                  }
                }}
              />
              <Button variante="outline" tamanho="sm" disabled={salvar.isPending} onClick={adicionar}>
                {salvar.isPending ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Plus className="h-4 w-4" />
                )}
                Adicionar
              </Button>

              {/* Testar aqui exercita o caminho de reabertura da janela de 24h agora, e não na
                  madrugada em que algo quebrou. */}
              <Button
                variante="ghost"
                tamanho="sm"
                disabled={testar.isPending || telefones.length === 0}
                onClick={async () => {
                  setAviso(null);
                  try {
                    const r = await testar.mutateAsync();
                    setAviso({
                      tipo: 'ok',
                      texto: `Mensagem de teste enviada para ${r.enviados} número(s).`,
                    });
                  } catch (e) {
                    setAviso({ tipo: 'erro', texto: extrairMensagemDeErro(e) });
                  }
                }}
              >
                {testar.isPending ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Send className="h-4 w-4" />
                )}
                Enviar teste
              </Button>
            </div>
          )}
        </>
      )}
    </section>
  );
}
