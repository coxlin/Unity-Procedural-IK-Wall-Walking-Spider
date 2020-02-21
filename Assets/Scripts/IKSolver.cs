﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct TargetInfo {
    public Vector3 position;
    public Vector3 normal;
    public bool comfortable;

    public TargetInfo(Vector3 m_position, Vector3 m_normal, bool m_comfortable = true) {
        position = m_position;
        normal = m_normal;
        comfortable = m_comfortable;
    }
}

public class IKSolver : MonoBehaviour {

    private static int maxIterations = 10;
    public static float tolerance = 0.05f;
    public static float minimumChangePerIteration = 0.001f;
    private static float weight = 1.0f;
    private static float footAngleToNormal = 20.0f; // 0 means parallel to ground (Orthogonal to plane normal)

    /*
     * Solves the IK Problem of the chain with given target using the CCD algorithm.
     * @param1 joints:  Contains all the Hinge Joints of the IK chain.
     * @param2 endEffector:  The end effector of the IK chain. It is not included in the list of hinge joints since it is not equipped with a AHingeJoint component.
     * @param3 target:       The target information the algorithm should solve for.
     * @param4 hasFoot:      If set to true, the last joint will adjust to the normal given by the target. 
     */
    public static void solveChainCCD(ref AHingeJoint[] joints, Transform endEffector, TargetInfo target, bool hasFoot = false, bool printDebugLogs = false) {
        int iteration = 0;
        float error = Vector3.Distance(target.position, endEffector.position);
        float oldError;
        float errorDelta;

        //If only the normal changes but my error is within tolerance, i will not adjust the normal here, maybe fix this
        while (iteration < maxIterations && error > tolerance) {

            for (int i = 0; i < joints.Length; i++) {
                //This line ensures that the we start with the last joint, but then chronologically, e.g. k= 4 0 1 2 3
                int k = mod((i - 1), joints.Length);
                solveJointCCD(ref joints[k], ref endEffector, ref target, hasFoot && k == joints.Length - 1);
            }
            iteration++;

            oldError = error;
            error = Vector3.Distance(target.position, endEffector.position);
            errorDelta = Mathf.Abs(oldError - error);
            if (errorDelta < minimumChangePerIteration) {
                if (printDebugLogs) Debug.Log("Only moved " + errorDelta + ". Therefore i give up solving.");
                break;
            }
        }

        if (printDebugLogs) {
            if (iteration == maxIterations) Debug.Log(endEffector.gameObject.name + " could not solve with " + iteration + " iterations. The error is " + error);
            if (iteration != maxIterations && iteration > 0) Debug.Log(endEffector.gameObject.name + " completed CCD with " + iteration + " iterations and an error of " + error);
        }
    }

    private static void solveJointCCD(ref AHingeJoint joint, ref Transform endEffector, ref TargetInfo target, bool adjustToTargetNormal) {
        Vector3 rotPoint = joint.getRotationPoint();
        Vector3 rotAxis = joint.getRotationAxis();
        Vector3 toEnd = Vector3.ProjectOnPlane((endEffector.position - rotPoint), rotAxis);
        Vector3 toTarget = Vector3.ProjectOnPlane(target.position - rotPoint, rotAxis);

        // If singularity, skip. ToEnd should never be zero in my configuration though
        if (toTarget == Vector3.zero || toEnd == Vector3.zero) return;

        float angle;

        //This is a special case, where i want the foot, that is the last joint of the chain to adjust to the normal it hit
        if (adjustToTargetNormal) {
            angle = footAngleToNormal + 90.0f - Vector3.SignedAngle(Vector3.ProjectOnPlane(target.normal, rotAxis), toEnd, rotAxis);
        }
        else {
            angle = Vector3.SignedAngle(toEnd, toTarget, rotAxis);
            angle *= weight;
            angle *= joint.getWeight();
            //float kValue = 1.0f / (joints.Length * error);
            //angle *= Mathf.Clamp(kValue, float.Epsilon, 1.0f); // k-Faktor //Have to update the error every forloop here
        }
        joint.applyRotation(angle);
    }

    /*
     * This coroutine is a copy paste of the original CCD solver above. It exists due to debug reasons.
     * It allows me to go through the iterations steps frame by frame and pause the editor.
     */
    public static IEnumerator solveChainCCDFrameByFrame(AHingeJoint[] joints, Transform endEffector, TargetInfo target, bool hasFoot = false, bool printDebugLogs = false) {
        int iteration = 0;
        float error = Vector3.Distance(target.position, endEffector.position);
        float oldError;
        float errorDelta;

        if (printDebugLogs) Debug.Log(endEffector.gameObject.name + " is starting the CCD solving process.");
        Debug.Break();
        yield return null;

        while (iteration < maxIterations && error > tolerance) {

            if (printDebugLogs) Debug.Log("Starting iteration " + iteration + " with an error of " + error);
            Debug.Break();
            yield return null;

            for (int i = 0; i < joints.Length; i++) {
                int k = mod((i - 1), joints.Length);

                // start: Not clean but for now just initialize variables again and draw stuff here
                Vector3 rotPoint = joints[k].getRotationPoint();
                Vector3 rotAxis = joints[k].getRotationAxis();
                Vector3 toEnd = Vector3.ProjectOnPlane((endEffector.position - rotPoint), rotAxis);
                Vector3 toTarget = Vector3.ProjectOnPlane(target.position - rotPoint, rotAxis);
                DebugShapes.DrawPlane(rotPoint, rotAxis, toTarget, 1.0f, Color.yellow);
                Debug.DrawLine(rotPoint, rotPoint + toTarget, Color.blue);
                Debug.DrawLine(rotPoint, rotPoint + toEnd, Color.red);
                // end

                if (printDebugLogs) Debug.Log("Iteration " + iteration + ", joint " + joints[k].gameObject.name + " gonna happen now.");
                Debug.Break();
                yield return null;

                solveJointCCD(ref joints[k], ref endEffector, ref target, hasFoot && k == joints.Length - 1);

                // start: Not clean but for now just initialize variables again and draw stuff here
                toEnd = Vector3.ProjectOnPlane((endEffector.position - rotPoint), rotAxis);
                DebugShapes.DrawPlane(rotPoint, rotAxis, toTarget, 1.0f, Color.yellow);
                Debug.DrawLine(rotPoint, rotPoint + toTarget, Color.blue);
                Debug.DrawLine(rotPoint, rotPoint + toEnd, Color.red);
                // end

                if (printDebugLogs) Debug.Log("Iteration " + iteration + ", joint " + joints[k].gameObject.name + " done.");
                Debug.Break();
                yield return null;
            }
            iteration++;

            oldError = error;
            error = Vector3.Distance(target.position, endEffector.position);
            errorDelta = Mathf.Abs(oldError - error);
            if (errorDelta < minimumChangePerIteration) {
                if (printDebugLogs) Debug.Log("Only moved " + errorDelta + ". Therefore i give up solving");
                Debug.Break();
                break;
            }
        }

        if (printDebugLogs) {
            if (error > tolerance) Debug.Log(endEffector.gameObject.name + " could not solve with " + iteration + " iterations. The error is " + error);
            else Debug.Log(endEffector.gameObject.name + " completed solving with " + iteration + " iterations and an error of " + error);
        }
        Debug.Break();
        yield return null;

    }

    // Slighly messy since Unity does not provide Matrix class so i had to work with two dimensional arrays and convert to Vector3 if needed
    public static void solveJacobianTranspose(ref AHingeJoint[] joints, Transform endEffector, TargetInfo target, bool hasFoot = false) {
        Vector3 error = target.position - endEffector.position;
        float[] err = new float[] { error.x, error.y, error.z };
        float[,] J = new float[3, joints.Length];
        float[,] JT = new float[joints.Length, 3];
        float[,] JJT = new float[3, 3];
        float[] JJTe = new float[3];
        float[] angleChange = new float[joints.Length];
        float alpha;

        int iteration = 0;
        while (iteration < maxIterations || error.magnitude < tolerance) {

            // Jacobian Form:
            //  a1  a2  a3  a4  a4 
            //  *   *   *   *   *   
            //  *   *   *   *   *  
            //  *   *   *   *   * 
            for (int k = 0; k < joints.Length; k++) {
                Vector3 rotAxis = joints[k].getRotationAxis();
                Vector3 cross = Vector3.Cross(rotAxis, endEffector.position - joints[k].getRotationPoint());
                J[0, k] = cross.x;
                J[1, k] = cross.y;
                J[2, k] = cross.z;
            }

            //Print Jacobian:
            string jacobianString = "";
            for (int i = 0; i < 3; i++) {
                for (int k = 0; k < joints.Length; k++) {
                    jacobianString += J[i, k] + " ";
                }
                jacobianString += "\n";
            }
            Debug.Log(jacobianString);


            // Jacobian Transpose Form:
            //  *   *   *       a1
            //  *   *   *       a2
            //  *   *   *       a3  
            //  *   *   *       a4
            //  *   *   *       a5
            Transpose(J, ref JT);

            // Jacobian times Jacobian Transpose has Form:
            //  *   *   *
            //  *   *   *
            //  *   *   *
            multiply(J, JT, ref JJT);

            // Calculate needed multiplications
            multiply(JJT, err, ref JJTe);
            Vector3 m_JJTe = new Vector3(JJTe[0], JJTe[1], JJTe[2]);

            //Calc the alpha value
            alpha = Vector3.Dot(error, m_JJTe) / Vector3.Dot(m_JJTe, m_JJTe);

            //Calc the change in angle
            multiply(JT, err, ref angleChange);
            multiply(ref angleChange, alpha);

            // Now apply the angle rotations
            for (int k = 0; k < joints.Length; k++) {
                joints[k].applyRotation(angleChange[k]);
            }

            error = target.position - endEffector.position; //Refresh the error so we can check if we are already close enough for the while loop check
            iteration++;
        }
    }

    // Multiplies A with its Transpose Matrix and saves the product in result
    private static void Transpose(float[,] A, ref float[,] result) {

        if (A.GetLength(1) != result.GetLength(0) || A.GetLength(0) != result.GetLength(1)) {
            Debug.Log("Transpose matrix not the right dimensions.");
            return;
        }

        for (int col = 0; col < A.GetLength(0); col++) {
            for (int row = 0; row < A.GetLength(1); row++) {
                result[row, col] = A[col, row];
            }
        }
    }

    // Matrix Multiplication
    private static void multiply(float[,] A, float[,] B, ref float[,] result) {
        if (A.GetLength(1) != B.GetLength(0) || result.GetLength(0) != A.GetLength(0) || result.GetLength(1) != B.GetLength(1)) {
            Debug.Log("Can't multiply these matrices.");
            return;
        }

        for (int row = 0; row < result.GetLength(0); row++) {
            for (int col = 0; col < result.GetLength(1); col++) {
                float sum = 0;

                for (int k = 0; k < A.GetLength(1); k++) {
                    sum += A[row, k] * B[k, col];
                }
                result[row, col] = sum;
            }
        }

    }

    // Matrix - Vector Multiplication
    private static void multiply(float[,] A, float[] B, ref float[] result) {
        if (A.GetLength(1) != B.Length || result.Length != A.GetLength(0)) {
            Debug.Log("Can't multiply these matrices.");
            return;
        }

        for (int row = 0; row < result.GetLength(0); row++) {
            float sum = 0;

            for (int k = 0; k < A.GetLength(1); k++) {
                sum += A[row, k] * B[k];
            }
            result[row] = sum;
        }
    }

    // Vector - Scalar Multiplication
    private static void multiply(ref float[] A, float a) {
        for (int k = 0; k < A.Length; k++) {
            A[k] *= a;
        }
    }

    // Implemented this, since the % operator in C# returns the remainder, which can be negative if n is.
    // This functions returns the modulo, that is a positive number.
    private static int mod(int n, int m) {
        return ((n % m) + m) % m;
    }

}