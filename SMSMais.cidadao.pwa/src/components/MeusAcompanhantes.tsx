import { useCallback, useEffect, useState } from 'react';
import { CheckCircle2, Trash2, UserPlus, Users } from 'lucide-react';
import { api, PARENTESCOS, type AcompanhanteCidadao, type ConsultaAcompanhante } from '@/lib/api';
import { classificarErro, extrairMensagemDeErro } from '@/lib/httpClient';
import { cpfParcial, cpfValido, digitosCpf, mascararCpf } from '@/lib/cpf';
import { Card, ErroCard, GhostButton, PrimaryButton, Skeleton } from '@/components/ui';

const CAMPO =
  'min-h-[52px] w-full rounded-2xl border border-areia bg-white px-4 text-base tabular-nums text-tinta shadow-carta transition placeholder:text-tinta-mute/40 focus:border-lagoa focus:outline-none focus:ring-4 focus:ring-lagoa/15';

/** dd/mm/aaaa enquanto digita. */
function mascararData(valor: string): string {
  const d = valor.replace(/\D/g, '').slice(0, 8);
  if (d.length <= 2) return d;
  if (d.length <= 4) return `${d.slice(0, 2)}/${d.slice(2)}`;
  return `${d.slice(0, 2)}/${d.slice(2, 4)}/${d.slice(4)}`;
}

/** dd/mm/aaaa → aaaa-mm-dd; null se a data não existe ou está no futuro. */
function paraIso(valor: string): string | null {
  const m = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(valor);
  if (!m) return null;
  const [, dia, mes, ano] = m;
  const d = new Date(Number(ano), Number(mes) - 1, Number(dia));
  if (
    d.getFullYear() !== Number(ano) ||
    d.getMonth() !== Number(mes) - 1 ||
    d.getDate() !== Number(dia) ||
    d > new Date() ||
    Number(ano) < 1900
  ) {
    return null;
  }
  return `${ano}-${mes}-${dia}`;
}

function rotuloParentesco(valor: string | null): string | null {
  return PARENTESCOS.find((p) => p.valor === valor)?.rotulo ?? null;
}

function mensagem(e: unknown): string {
  if (classificarErro(e).status === 429) {
    return 'Você chegou ao limite de consultas de hoje. Tente de novo amanhã ou fale com a equipe do transporte.';
  }
  return extrairMensagemDeErro(e);
}

/**
 * Acompanhantes do paciente no transporte. O próprio paciente cadastra pelo CPF e pela data de
 * nascimento — o sistema confere e traz o nome. Quem a equipe cadastrou só a equipe tira.
 */
export function MeusAcompanhantes() {
  const [lista, setLista] = useState<AcompanhanteCidadao[] | null>(null);
  const [erroLista, setErroLista] = useState<string | null>(null);
  const [adicionando, setAdicionando] = useState(false);
  const [confirmandoId, setConfirmandoId] = useState<string | null>(null);
  const [removendo, setRemovendo] = useState(false);
  const [erroRemover, setErroRemover] = useState<string | null>(null);

  const carregar = useCallback(() => {
    setErroLista(null);
    api
      .acompanhantes()
      .then(setLista)
      .catch((e) => setErroLista(extrairMensagemDeErro(e)));
  }, []);

  useEffect(() => carregar(), [carregar]);

  async function remover(a: AcompanhanteCidadao) {
    setErroRemover(null);
    setRemovendo(true);
    try {
      await api.removerAcompanhante(a.id);
      setConfirmandoId(null);
      carregar();
    } catch (e) {
      setErroRemover(mensagem(e));
    } finally {
      setRemovendo(false);
    }
  }

  return (
    <section className="mt-8">
      <div className="mb-3 flex items-center justify-between gap-3">
        <h2 className="flex items-center gap-2 font-display text-lg font-semibold text-tinta">
          <Users className="h-5 w-5 text-marica" /> Meus acompanhantes
        </h2>
      </div>
      <p className="mb-3 text-sm text-tinta-mute">
        Quem pode ir com você nas viagens. Em cada viagem vai 1 acompanhante (2 só com liberação da equipe).
      </p>

      {erroLista ? (
        <ErroCard mensagem={erroLista} aoTentar={carregar} />
      ) : lista === null ? (
        <Skeleton className="h-16 w-full" />
      ) : lista.length === 0 ? (
        <p className="rounded-2xl border border-dashed border-areia px-4 py-5 text-center text-sm text-tinta-mute">
          Nenhum acompanhante cadastrado ainda.
        </p>
      ) : (
        <ul className="space-y-2">
          {lista.map((a) => (
            <li key={a.id}>
              <Card className="p-4">
                <div className="flex items-center justify-between gap-3">
                  <div className="min-w-0">
                    <p className="truncate font-semibold text-tinta">{a.nome}</p>
                    <p className="text-xs text-tinta-mute">
                      {rotuloParentesco(a.parentesco) ? `${rotuloParentesco(a.parentesco)} · ` : ''}CPF {cpfParcial(a.cpf)}
                      {a.origem === 'Painel' ? ' · cadastrado pela equipe' : ''}
                    </p>
                  </div>
                  {a.origem === 'App' && confirmandoId !== a.id ? (
                    <button
                      type="button"
                      onClick={() => { setConfirmandoId(a.id); setErroRemover(null); }}
                      className="rounded-xl p-2 text-red-700 active:bg-red-50"
                      aria-label={`Tirar ${a.nome} da lista`}
                    >
                      <Trash2 className="h-5 w-5" />
                    </button>
                  ) : null}
                </div>
                {confirmandoId === a.id ? (
                  <div className="mt-3 space-y-2 rounded-xl bg-papel p-3">
                    <p className="text-sm text-tinta">Tirar {a.nome} da sua lista?</p>
                    {erroRemover ? <p role="alert" className="text-sm text-red-700">{erroRemover}</p> : null}
                    <div className="flex gap-2">
                      <GhostButton className="flex-1" onClick={() => setConfirmandoId(null)}>Não</GhostButton>
                      <PrimaryButton className="flex-1" carregando={removendo} onClick={() => remover(a)}>
                        Tirar da lista
                      </PrimaryButton>
                    </div>
                  </div>
                ) : null}
              </Card>
            </li>
          ))}
        </ul>
      )}

      {adicionando ? (
        <FormularioAcompanhante
          aoCancelar={() => setAdicionando(false)}
          aoConcluir={() => {
            setAdicionando(false);
            carregar();
          }}
        />
      ) : (
        <GhostButton className="mt-3 w-full" onClick={() => setAdicionando(true)}>
          <UserPlus className="h-4 w-4" /> Adicionar acompanhante
        </GhostButton>
      )}
    </section>
  );
}

function FormularioAcompanhante({ aoCancelar, aoConcluir }: { aoCancelar: () => void; aoConcluir: () => void }) {
  const [cpf, setCpf] = useState('');
  const [nascimento, setNascimento] = useState('');
  const [parentesco, setParentesco] = useState('');
  const [encontrado, setEncontrado] = useState<ConsultaAcompanhante | null>(null);
  const [erro, setErro] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  const nascimentoIso = paraIso(nascimento);
  const cpfCompleto = digitosCpf(cpf).length === 11;
  const cpfErrado = cpfCompleto && !cpfValido(cpf);
  const podeConferir = cpfCompleto && !cpfErrado && nascimentoIso !== null;

  async function conferir() {
    if (!nascimentoIso) return;
    setErro(null);
    setCarregando(true);
    try {
      setEncontrado(await api.consultarAcompanhante(digitosCpf(cpf), nascimentoIso));
    } catch (e) {
      setErro(mensagem(e));
    } finally {
      setCarregando(false);
    }
  }

  async function confirmar() {
    if (!nascimentoIso) return;
    setErro(null);
    setCarregando(true);
    try {
      await api.adicionarAcompanhante(digitosCpf(cpf), nascimentoIso, parentesco || null);
      aoConcluir();
    } catch (e) {
      setErro(mensagem(e));
    } finally {
      setCarregando(false);
    }
  }

  return (
    <Card className="mt-3 space-y-3 p-4">
      {encontrado ? (
        <div>
          <p className="flex items-center gap-2 text-sm font-medium text-lagoa-escuro">
            <CheckCircle2 className="h-4 w-4" /> Encontramos
          </p>
          <p className="mt-1 text-lg font-semibold text-tinta">{encontrado.nome}</p>
          {encontrado.jaCadastrado ? (
            <p className="mt-1 text-sm text-tinta-mute">Essa pessoa já está na sua lista.</p>
          ) : (
            <p className="mt-1 text-sm text-tinta-mute">É essa pessoa? Confirme para ela entrar na sua lista.</p>
          )}
        </div>
      ) : (
        <>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-tinta">CPF do acompanhante</span>
            <input
              className={CAMPO}
              inputMode="numeric"
              autoComplete="off"
              placeholder="000.000.000-00"
              value={mascararCpf(cpf)}
              onChange={(e) => setCpf(e.target.value)}
            />
            {cpfErrado ? <span className="mt-1 block text-xs text-red-700">CPF inválido. Confira os números.</span> : null}
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-tinta">Data de nascimento</span>
            <input
              className={CAMPO}
              inputMode="numeric"
              placeholder="dd/mm/aaaa"
              value={nascimento}
              onChange={(e) => setNascimento(mascararData(e.target.value))}
            />
          </label>
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-tinta">Parentesco (opcional)</span>
            <select className={CAMPO} value={parentesco} onChange={(e) => setParentesco(e.target.value)}>
              <option value="">Não informar</option>
              {PARENTESCOS.map((p) => (
                <option key={p.valor} value={p.valor}>{p.rotulo}</option>
              ))}
            </select>
          </label>
        </>
      )}

      {erro ? <p role="alert" className="rounded-xl bg-red-50 px-3 py-2 text-sm text-red-700">{erro}</p> : null}

      <div className="flex gap-2">
        {encontrado ? (
          <>
            <GhostButton className="flex-1" onClick={() => { setEncontrado(null); setErro(null); }}>
              Corrigir
            </GhostButton>
            {encontrado.jaCadastrado ? (
              <PrimaryButton className="flex-1" onClick={aoConcluir}>Fechar</PrimaryButton>
            ) : (
              <PrimaryButton className="flex-1" carregando={carregando} onClick={confirmar}>
                Confirmar
              </PrimaryButton>
            )}
          </>
        ) : (
          <>
            <GhostButton className="flex-1" onClick={aoCancelar}>Cancelar</GhostButton>
            <PrimaryButton className="flex-1" carregando={carregando} disabled={!podeConferir} onClick={conferir}>
              Conferir
            </PrimaryButton>
          </>
        )}
      </div>
    </Card>
  );
}
