import { useEffect, useState } from 'react';
import { BadgeCheck, Check } from 'lucide-react';
import { api } from '@/lib/api';
import { extrairMensagemDeErro } from '@/lib/httpClient';
import { usePerfil } from '@/store/perfil';
import { formatarCpf } from '@/components/AppShell';
import { AvatarUploader } from '@/components/AvatarUploader';
import { Card, ErroCard, Field, PrimaryButton, SectionHeader, Spinner } from '@/components/ui';

export function Perfil() {
  const perfil = usePerfil((s) => s.perfil);
  const carregando = usePerfil((s) => s.carregando);
  const erroPerfil = usePerfil((s) => s.erro);
  const carregar = usePerfil((s) => s.carregar);
  const setPerfil = usePerfil((s) => s.setPerfil);

  const [email, setEmail] = useState('');
  const [celular, setCelular] = useState('');
  const [residencial, setResidencial] = useState('');
  const [salvando, setSalvando] = useState(false);
  const [salvandoFoto, setSalvandoFoto] = useState(false);
  const [trocando, setTrocando] = useState(false);
  const [ok, setOk] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  useEffect(() => {
    if (!perfil && !carregando) void carregar();
  }, [perfil, carregando, carregar]);

  useEffect(() => {
    if (!perfil) return;
    setEmail(perfil.email ?? '');
    setCelular(perfil.telefoneCelular ?? perfil.telefonePrincipal ?? '');
    setResidencial(perfil.telefoneResidencial ?? '');
  }, [perfil]);

  async function trocarFoto(base64: string) {
    setSalvandoFoto(true);
    setErro(null);
    try {
      await api.salvarFoto(base64);
      if (perfil) setPerfil({ ...perfil, fotoBase64: base64 });
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setSalvandoFoto(false);
    }
  }

  async function salvar(e: React.FormEvent) {
    e.preventDefault();
    setSalvando(true);
    setErro(null);
    setOk(false);
    try {
      await api.salvarContato({
        email: email.trim() || null,
        telefonePrincipal: celular.trim() || null,
        telefoneCelular: celular.trim() || null,
        telefoneResidencial: residencial.trim() || null,
      });
      if (perfil)
        setPerfil({
          ...perfil,
          email: email.trim() || null,
          telefoneCelular: celular.trim() || null,
          telefonePrincipal: celular.trim() || null,
          telefoneResidencial: residencial.trim() || null,
        });
      setOk(true);
      setTimeout(() => setOk(false), 2500);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setSalvando(false);
    }
  }

  if (!perfil) {
    if (erroPerfil && !carregando) {
      return (
        <div className="animate-rise pt-4">
          <SectionHeader title="Meu perfil" />
          <ErroCard mensagem={`Não foi possível carregar seu perfil. ${erroPerfil}`} aoTentar={carregar} />
        </div>
      );
    }
    return (
      <div className="grid place-items-center py-24">
        <Spinner />
      </div>
    );
  }

  const nome = perfil.nomeSocial || perfil.nome;

  return (
    <div className="animate-rise space-y-6">
      <div className="flex flex-col items-center pt-2">
        <AvatarUploader valor={perfil.fotoBase64} nome={nome} aoMudar={trocarFoto} salvando={salvandoFoto} />
        <h1 className="mt-4 text-center font-display text-2xl font-semibold text-tinta">{nome}</h1>
        <p className="text-sm text-tinta-mute">{salvandoFoto ? 'Salvando foto…' : 'Toque na câmera para trocar a foto'}</p>
      </div>

      {/* Dados oficiais (somente leitura) */}
      <Card className="overflow-hidden">
        <div className="flex items-center gap-2 border-b border-areia bg-papel/60 px-4 py-3">
          <BadgeCheck className="h-4 w-4 text-lagoa" />
          <span className="text-xs font-semibold uppercase tracking-wider text-tinta-mute">
            Cadastro oficial
          </span>
        </div>
        <dl className="divide-y divide-areia">
          <Linha rotulo="Nome" valor={perfil.nome} />
          <Linha rotulo="CPF" valor={formatarCpf(perfil.cpf)} />
          {perfil.cns && <Linha rotulo="Cartão SUS" valor={perfil.cns} />}
          {perfil.dataNascimento && <Linha rotulo="Nascimento" valor={formatarData(perfil.dataNascimento)} />}
        </dl>
      </Card>

      {/* Contatos (editáveis) */}
      <form onSubmit={salvar} className="space-y-4">
        <SectionHeader eyebrow="Como falamos com você" title="Contatos" />
        <Field
          label="E-mail"
          type="email"
          inputMode="email"
          autoComplete="email"
          placeholder="Seu e-mail"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
        />
        {/* Celular de contato: troca só por OTP (confirma o código no número novo). */}
        <div className="rounded-2xl border border-areia bg-white p-4">
          <div className="flex items-center justify-between gap-3">
            <div className="min-w-0">
              <p className="text-xs font-medium text-tinta-mute">Celular / WhatsApp</p>
              <p className="truncate text-sm font-semibold text-tinta">{celular || 'Não informado'}</p>
            </div>
            <button
              type="button"
              onClick={() => setTrocando(true)}
              className="shrink-0 rounded-xl bg-marica/10 px-3.5 py-2 text-sm font-semibold text-marica transition active:scale-95"
            >
              Trocar
            </button>
          </div>
          <p className="mt-1.5 text-xs text-tinta-mute">
            É neste número que enviamos seu código de acesso e avisos. Para trocar, você confirma um
            código enviado ao número novo — assim não corremos o risco de perder o contato.
          </p>
        </div>
        <Field
          label="Telefone fixo (opcional)"
          type="tel"
          inputMode="tel"
          placeholder="(21) 0000-0000"
          value={residencial}
          onChange={(e) => setResidencial(e.target.value)}
        />
        {erro && <p className="text-sm text-marica">{erro}</p>}
        <PrimaryButton type="submit" carregando={salvando}>
          {ok ? (
            <>
              <Check className="h-5 w-5" /> Salvo
            </>
          ) : (
            'Salvar alterações'
          )}
        </PrimaryButton>
      </form>

      {trocando && (
        <TrocaCelularModal
          aoFechar={() => setTrocando(false)}
          aoTrocar={(novo) => {
            setCelular(novo);
            setTrocando(false);
            void carregar();
          }}
        />
      )}
    </div>
  );
}

/**
 * Troca do celular em 2 passos: (1) informar o número novo → recebe um código nele;
 * (2) digitar o código → só então a troca é salva. Evita cadastrar número errado e
 * ficar sem contato.
 */
function TrocaCelularModal({
  aoFechar,
  aoTrocar,
}: {
  aoFechar: () => void;
  aoTrocar: (novoNumero: string) => void;
}) {
  const [passo, setPasso] = useState<'numero' | 'codigo'>('numero');
  const [numero, setNumero] = useState('');
  const [codigo, setCodigo] = useState('');
  const [mascara, setMascara] = useState<string | null>(null);
  const [ocupado, setOcupado] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  async function enviar() {
    if (numero.replace(/\D/g, '').length < 10) {
      setErro('Informe um celular com DDD.');
      return;
    }
    setOcupado(true);
    setErro(null);
    try {
      const r = await api.solicitarOtpContato(numero.trim());
      setMascara(r.mascara);
      setPasso('codigo');
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  async function confirmar() {
    if (codigo.replace(/\D/g, '').length < 4) {
      setErro('Digite o código recebido.');
      return;
    }
    setOcupado(true);
    setErro(null);
    try {
      const r = await api.confirmarContato(numero.trim(), codigo.trim());
      aoTrocar(r.numero);
    } catch (e) {
      setErro(extrairMensagemDeErro(e));
    } finally {
      setOcupado(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 mx-auto flex max-w-[460px] items-end" role="dialog" aria-modal="true">
      <button type="button" aria-label="Fechar" onClick={aoFechar} className="absolute inset-0 animate-fade-in bg-tinta/40" />
      <div className="relative w-full animate-rise rounded-t-3xl bg-papel p-6 shadow-2xl">
        <SectionHeader eyebrow="Segurança" title="Trocar celular" />

        {passo === 'numero' ? (
          <div className="mt-4 space-y-4">
            <Field
              label="Novo celular / WhatsApp"
              type="tel"
              inputMode="tel"
              autoComplete="tel"
              placeholder="(21) 90000-0000"
              value={numero}
              onChange={(e) => setNumero(e.target.value)}
              hint="Enviaremos um código por WhatsApp para este número."
            />
            {erro && <p className="text-sm text-marica">{erro}</p>}
            <PrimaryButton onClick={enviar} carregando={ocupado}>
              Enviar código
            </PrimaryButton>
          </div>
        ) : (
          <div className="mt-4 space-y-4">
            <p className="text-sm text-tinta-mute">
              Enviamos um código para {mascara ?? 'o número novo'}. Digite-o para confirmar a troca.
            </p>
            <Field
              label="Código"
              type="tel"
              inputMode="numeric"
              placeholder="000000"
              value={codigo}
              onChange={(e) => setCodigo(e.target.value)}
            />
            {erro && <p className="text-sm text-marica">{erro}</p>}
            <PrimaryButton onClick={confirmar} carregando={ocupado}>
              Confirmar troca
            </PrimaryButton>
            <button
              type="button"
              onClick={() => {
                setPasso('numero');
                setCodigo('');
                setErro(null);
              }}
              className="w-full text-center text-sm text-tinta-mute underline"
            >
              Corrigir o número
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

function Linha({ rotulo, valor }: { rotulo: string; valor: string }) {
  return (
    <div className="flex items-center justify-between gap-4 px-4 py-3">
      <dt className="text-sm text-tinta-mute">{rotulo}</dt>
      <dd className="text-right text-sm font-medium text-tinta">{valor}</dd>
    </div>
  );
}

function formatarData(iso: string): string {
  const [a, m, d] = iso.split('-');
  return d && m && a ? `${d}/${m}/${a}` : iso;
}
