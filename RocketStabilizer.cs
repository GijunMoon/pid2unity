using UnityEngine;
using UnityEngine.UI;

public class RocketStabilizer : MonoBehaviour
{
    public Rigidbody rocketBody; // 로켓 바디의 리지드바디 컴포넌트
    public Rigidbody reactionWheelBody; // 리액션 휠의 리지드바디 컴포넌트
    public float kp = 19.1f; // 비례 상수
    public float kd = 9.5f;  // 미분 상수
    public float ki = 0.0f;  // 적분 상수
    public float targetAngularVelocity = 0.0f; // 목표 각속도 (rad/s)
    public float manualTorqueMultiplier = 2000.0f; // 수동 회전 토크 배율

    public Text pidOutputText; // PID 출력 UI 텍스트
    public Text currentRPMText; // 현재 RPM UI 텍스트
    public InputField kpInputField; // UI에서 kp 값을 입력받기 위한 인풋 필드
    public InputField kdInputField; // UI에서 kd 값을 입력받기 위한 인풋 필드
    public InputField kiInputField; // UI에서 ki 값을 입력받기 위한 인풋 필드
    public InputField targetAngularVelocityInputField; // 목표 각속도 입력 필드

    private float previousError = 0.0f; // 이전 프레임에서의 각도 에러
    private float currentError = 0.0f;  // 현재 각도 에러
    private float integral = 0.0f;       // 적분 항
    private float J_rocket;             // 로켓 관성 모멘트
    private float J_wheel;              // 리액션 휠 관성 모멘트
    private float maxRPM = 4000.0f;     // 최대 RPM 제한
    private float maxTorque = 11.0f;    // 최대 토크 제한 (Nm)
    private float currentRPM = 0.0f;    // 현재 RPM

    void Start()
    {
        // 로켓 및 리액션 휠의 관성 모멘트 계산
        float M_rocket = 5.0f;     // 로켓 질량 (kg)
        float R_rocket = 0.06f;    // 로켓 반지름 (m)
        J_rocket = 0.5f * M_rocket * R_rocket * R_rocket; // 로켓 관성 모멘트

        float M_wheel = 0.370f;    // 리액션 휠 질량 (kg)
        float R_wheel = 0.05f;     // 리액션 휠 반지름 (m)
        J_wheel = 0.5f * M_wheel * R_wheel * R_wheel; // 리액션 휠 관성 모멘트

        // UI 인풋 필드 이벤트 등록
        if (kpInputField != null)
        {
            kpInputField.onEndEdit.AddListener(delegate { UpdateKpValue(); });
        }
        if (kdInputField != null)
        {
            kdInputField.onEndEdit.AddListener(delegate { UpdateKdValue(); });
        }
        if (kiInputField != null)
        {
            kiInputField.onEndEdit.AddListener(delegate { UpdateKiValue(); });
        }
        if (targetAngularVelocityInputField != null)
        {
            targetAngularVelocityInputField.onEndEdit.AddListener(delegate { UpdateTargetAngularVelocity(); });
        }
    }

    void FixedUpdate()
    {
        // 현재 로켓의 y축 회전 각도 에러 계산
        currentError = -rocketBody.rotation.eulerAngles.y; // y축 각도 에러 (단위: 도)
        if (currentError > 180.0f)
        {
            currentError -= 360.0f; // -180 ~ 180도 범위로 변환
        }

        // 제어 입력이 필요하지 않으면 적분 항 초기화 및 종료
        if (Mathf.Abs(currentError) < 0.1f && Mathf.Abs(rocketBody.angularVelocity.y) < 0.1f)
        {
            integral = 0.0f;
            previousError = currentError;
            return;
        }

        // 적분 항 업데이트
        integral += currentError * Time.fixedDeltaTime;

        // 미분 항 계산
        float derivative = (currentError - previousError) / Time.fixedDeltaTime;

        // 제어 입력 계산 (토크)
        float controlTorque = kp * currentError + kd * derivative + ki * integral;

        // 최대 토크 제한 적용
        controlTorque = Mathf.Clamp(controlTorque, -maxTorque, maxTorque);

        // 리액션 휠에 반대 방향 토크 적용 (뉴턴의 작용-반작용 법칙 적용)
        float wheelTorque = -controlTorque;

        // 최대 RPM 제한 적용을 위한 각속도 변환 및 조건 처리
        float targetAngularVelocity = maxRPM * 2.0f * Mathf.PI / 60.0f; // 최대 RPM에 해당하는 각속도
        if (Mathf.Abs(reactionWheelBody.angularVelocity.y) > targetAngularVelocity)
        {
            wheelTorque = -Mathf.Sign(reactionWheelBody.angularVelocity.y) * maxTorque; // 역방향으로 회전하도록 설정
        }

        // 로켓 바디와 리액션 휠에 각각 토크 적용 (반작용)
        rocketBody.AddTorque(Vector3.up * controlTorque);
        reactionWheelBody.AddTorque(Vector3.up * wheelTorque);

        // 현재 RPM 계산 (각속도 -> RPM 변환)
        currentRPM = Mathf.Abs(reactionWheelBody.angularVelocity.y) * 60.0f / (2.0f * Mathf.PI);

        // 현재 에러 저장
        previousError = currentError;

        // 수동 회전 입력 (사용자가 로켓을 돌리는 과정 구현)
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            rocketBody.AddTorque(Vector3.up * -manualTorqueMultiplier);
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            rocketBody.AddTorque(Vector3.up * manualTorqueMultiplier);
        }

        // UI 업데이트
        if (pidOutputText != null)
        {
            pidOutputText.text = "PID Output: " + controlTorque.ToString("F2") + " Nm";
        }
        if (currentRPMText != null)
        {
            currentRPMText.text = "Current RPM: " + currentRPM.ToString("F2") + " RPM";
        }
    }

    // UI 인풋 필드를 통해 PID 상수 및 목표 각속도 업데이트
    public void UpdateKpValue()
    {
        if (float.TryParse(kpInputField.text, out float newValue))
        {
            kp = newValue;
        }
    }

    public void UpdateKdValue()
    {
        if (float.TryParse(kdInputField.text, out float newValue))
        {
            kd = newValue;
        }
    }

    public void UpdateKiValue()
    {
        if (float.TryParse(kiInputField.text, out float newValue))
        {
            ki = newValue;
        }
    }

    public void UpdateTargetAngularVelocity()
    {
        if (float.TryParse(targetAngularVelocityInputField.text, out float newValue))
        {
            targetAngularVelocity = newValue;
        }
    }
}
