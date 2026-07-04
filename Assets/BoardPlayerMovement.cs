using UnityEngine;

public class BoardPlayerMovement : MonoBehaviour
{
    [Header("Movimiento en tablero")]
    public float velocidadMovimiento = 0.25f;
    public float deadZoneStick = 0.18f;
    public float margenBordeTablero = 0.12f;
    public float alturaSobreSuelo = 0.02f;
    public float velocidadAjusteVertical = 0.35f;

    [Header("Separacion de fichas")]
    public float radioJugador = 0.06f;
    public float margenSeparacionFicha = 0.025f;
    public float velocidadEmpujeLateral = 0.9f;
    public float intervaloActualizarFichas = 0.5f;

    [Header("Giro")]
    public bool usarSnapTurn = true;
    public float gradosSnapTurn = 30f;
    public float cooldownSnapTurn = 0.32f;
    public float deadZoneGiro = 0.55f;

    bool movimientoActivo;
    float proximoSnapTurn;
    Oculus.Interaction.Locomotion.CharacterController charController;
    Oculus.Interaction.Locomotion.FirstPersonLocomotor locomotor;
    bool locomotorEstadoPrevio;
    CampeonCombat[] fichasCache = new CampeonCombat[0];
    float proximaActualizacionFichas;
    float offsetCamaraSobreSuelo;
    bool alturaCamaraCalibrada;

    void Awake()
    {
        CacheComponentesMeta();
    }

    void OnDisable()
    {
        SetMovimientoActivo(false);
    }

    void Update()
    {
        CombatManager cm = CombatManager.Instance;
        if (!movimientoActivo || cm == null || !cm.EnCombate || cm.CombateTerminado || cm.TransicionCombateActiva)
            return;

        AplicarMovimiento(cm);
        AplicarSnapTurn();
    }

    public void SetMovimientoActivo(bool activo)
    {
        if (movimientoActivo == activo) return;

        movimientoActivo = activo;
        CacheComponentesMeta();

        if (locomotor != null)
        {
            if (activo)
            {
                locomotorEstadoPrevio = locomotor.enabled;
                locomotor.enabled = false;
            }
            else
            {
                locomotor.enabled = locomotorEstadoPrevio;
            }
        }

        if (activo)
            CalibrarAlturaCamara();
        else
            alturaCamaraCalibrada = false;
    }

    void CacheComponentesMeta()
    {
        if (charController == null)
            charController = GetComponentInChildren<Oculus.Interaction.Locomotion.CharacterController>(true);
        if (locomotor == null)
            locomotor = GetComponentInChildren<Oculus.Interaction.Locomotion.FirstPersonLocomotor>(true);
    }

    void AplicarMovimiento(CombatManager cm)
    {
        Vector2 input = LeerMovimiento();
        bool tieneInput = input.sqrMagnitude >= deadZoneStick * deadZoneStick;
        input = tieneInput ? Vector2.ClampMagnitude(input, 1f) : Vector2.zero;
        Transform cam = Camera.main != null ? Camera.main.transform : transform;

        Vector3 forward = cam.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        forward.Normalize();

        Vector3 right = cam.right;
        right.y = 0f;
        if (right.sqrMagnitude < 0.0001f) right = transform.right;
        right.Normalize();

        Vector3 delta = (right * input.x + forward * input.y) * velocidadMovimiento * Time.deltaTime;
        Vector3 posicionJugador = ObtenerPosicionJugador();
        Vector3 destinoJugador = posicionJugador + delta;
        destinoJugador = ResolverSeparacionLateral(destinoJugador, delta);
        destinoJugador = cm.LimitarPosicionAlTablero(destinoJugador, margenBordeTablero);

        float sueloY;
        bool tieneSuelo = cm.TryObtenerSueloTablero(destinoJugador, out sueloY);
        if (tieneSuelo)
        {
            if (!alturaCamaraCalibrada)
                CalibrarAlturaCamara();

            float yObjetivo = sueloY + offsetCamaraSobreSuelo;
            destinoJugador.y = Mathf.MoveTowards(
                posicionJugador.y,
                yObjetivo,
                Mathf.Max(0.01f, velocidadAjusteVertical) * Time.deltaTime);
        }

        Vector3 desplazamiento = destinoJugador - posicionJugador;
        if (desplazamiento.sqrMagnitude < 0.0000001f)
            return;

        transform.position += desplazamiento;
        SincronizarControladorMeta(destinoJugador, tieneSuelo, sueloY);
    }

    Vector3 ObtenerPosicionJugador()
    {
        if (Camera.main != null)
            return Camera.main.transform.position;
        return transform.position;
    }

    void CalibrarAlturaCamara()
    {
        CombatManager cm = CombatManager.Instance;
        Vector3 posicionJugador = ObtenerPosicionJugador();
        float sueloY;
        if (cm != null && cm.TryObtenerSueloTablero(posicionJugador, out sueloY))
        {
            offsetCamaraSobreSuelo = Mathf.Clamp(
                posicionJugador.y - sueloY,
                0.02f,
                0.4f);
            alturaCamaraCalibrada = true;
        }
        else
        {
            offsetCamaraSobreSuelo = Mathf.Max(alturaSobreSuelo, 0.02f);
            alturaCamaraCalibrada = false;
        }
    }

    Vector3 ResolverSeparacionLateral(Vector3 destino, Vector3 direccionMovimiento)
    {
        ActualizarCacheFichasSiHaceFalta();

        Vector3 resultado = destino;
        foreach (CampeonCombat ficha in fichasCache)
        {
            if (ficha == null || ficha.EstaMuerto || !ficha.gameObject.activeInHierarchy)
                continue;

            Vector3 desdeFicha = resultado - ficha.transform.position;
            desdeFicha.y = 0f;

            float distanciaMinima = Mathf.Max(0.01f,
                radioJugador + ficha.radioCuerpo + margenSeparacionFicha);
            float distancia = desdeFicha.magnitude;
            if (distancia >= distanciaMinima)
                continue;

            Vector3 direccionSalida;
            if (distancia > 0.001f)
            {
                direccionSalida = desdeFicha / distancia;
            }
            else
            {
                Vector3 avance = direccionMovimiento;
                avance.y = 0f;
                if (avance.sqrMagnitude < 0.0001f)
                    avance = transform.forward;

                direccionSalida = Vector3.Cross(Vector3.up, avance.normalized);
            }

            float penetracion = distanciaMinima - distancia;
            float empujeMaximo = Mathf.Max(0.01f, velocidadEmpujeLateral) * Time.deltaTime;
            resultado += direccionSalida * Mathf.Min(penetracion, empujeMaximo);
        }

        resultado.y = destino.y;
        return resultado;
    }

    void ActualizarCacheFichasSiHaceFalta()
    {
        if (Time.time < proximaActualizacionFichas && fichasCache != null)
            return;

        fichasCache = FindObjectsOfType<CampeonCombat>();
        proximaActualizacionFichas = Time.time + Mathf.Max(0.1f, intervaloActualizarFichas);
    }

    Vector2 LeerMovimiento()
    {
        Vector2 input = Vector2.zero;

        try
        {
            input = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
        }
        catch
        {
            input = Vector2.zero;
        }

#if UNITY_EDITOR
        float x = 0f;
        float y = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
        Vector2 teclado = new Vector2(x, y);
        if (teclado.sqrMagnitude > input.sqrMagnitude)
            input = teclado.normalized;
#endif

        return input;
    }

    void AplicarSnapTurn()
    {
        if (!usarSnapTurn || Time.time < proximoSnapTurn) return;

        float giro = LeerGiro();
        if (Mathf.Abs(giro) < deadZoneGiro) return;

        float direccion = Mathf.Sign(giro);
        Vector3 pivoteJugador = ObtenerPosicionJugador();
        transform.Rotate(Vector3.up, gradosSnapTurn * direccion, Space.World);
        transform.position += pivoteJugador - ObtenerPosicionJugador();
        proximoSnapTurn = Time.time + cooldownSnapTurn;
        SincronizarControladorMeta();
    }

    float LeerGiro()
    {
        float giro = 0f;
        try
        {
            giro = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch).x;
        }
        catch
        {
            giro = 0f;
        }

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Q)) giro = -1f;
        if (Input.GetKeyDown(KeyCode.E)) giro = 1f;
#endif

        return giro;
    }

    void SincronizarControladorMeta(
        Vector3 posicionJugador = default(Vector3),
        bool tieneSuelo = false,
        float sueloY = 0f)
    {
        if (charController == null) return;

        if (posicionJugador == default(Vector3))
            posicionJugador = ObtenerPosicionJugador();

        if (!tieneSuelo && CombatManager.Instance != null)
            tieneSuelo = CombatManager.Instance.TryObtenerSueloTablero(posicionJugador, out sueloY);

        if (tieneSuelo)
        {
            charController.transform.position = new Vector3(
                posicionJugador.x,
                sueloY,
                posicionJugador.z);
        }

        Physics.SyncTransforms();
        Pose pose = new Pose(charController.transform.position, charController.transform.rotation);
        charController.SetPose(in pose);
        charController.TryGround(0.1f);
    }
}
