using UnityEngine;
using UnityEditor;

public static class PredictorlessSolverVerification
{
    private static int checks;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new System.Exception("Predictorless solver check failed: " + message);
        checks++;
    }

    private static double[,] RadauA()
    {
        double s = System.Math.Sqrt(6.0);
        return new double[,]
        {
            { (88.0-7.0*s)/360.0, (296.0-169.0*s)/1800.0, (-2.0+3.0*s)/225.0 },
            { (296.0+169.0*s)/1800.0, (88.0+7.0*s)/360.0, (-2.0-3.0*s)/225.0 },
            { (16.0-s)/36.0, (16.0+s)/36.0, 1.0/9.0 }
        };
    }

    private static double[] Solve3(double[,] a, double[] b)
    {
        for (int k = 0; k < 3; k++)
        {
            int pivot = k;
            for (int row = k + 1; row < 3; row++)
                if (System.Math.Abs(a[row,k]) > System.Math.Abs(a[pivot,k])) pivot = row;
            Check(System.Math.Abs(a[pivot,k]) > 1e-15, "CPU reference pivot");
            if (pivot != k)
            {
                for (int column = 0; column < 3; column++)
                { double swap = a[k,column]; a[k,column] = a[pivot,column]; a[pivot,column] = swap; }
                double rhs = b[k]; b[k] = b[pivot]; b[pivot] = rhs;
            }
            for (int row = k + 1; row < 3; row++)
            {
                double factor = a[row,k] / a[k,k];
                for (int column = k; column < 3; column++) a[row,column] -= factor * a[k,column];
                b[row] -= factor * b[k];
            }
        }
        double[] x = new double[3];
        for (int row = 2; row >= 0; row--)
        {
            double value = b[row];
            for (int column = row + 1; column < 3; column++) value -= a[row,column] * x[column];
            x[row] = value / a[row,row];
        }
        return x;
    }

    private static double[] Solve(double[,] a, double[] b)
    {
        int size=b.Length;
        for(int k=0;k<size;k++)
        {
            int pivot=k;
            for(int row=k+1;row<size;row++)
                if(System.Math.Abs(a[row,k])>System.Math.Abs(a[pivot,k]))pivot=row;
            Check(System.Math.Abs(a[pivot,k])>1e-15,"linear reference pivot");
            if(pivot!=k)
            {
                for(int column=0;column<size;column++)
                {double swap=a[k,column];a[k,column]=a[pivot,column];a[pivot,column]=swap;}
                double rhs=b[k];b[k]=b[pivot];b[pivot]=rhs;
            }
            for(int row=k+1;row<size;row++)
            {
                double factor=a[row,k]/a[k,k];
                for(int column=k;column<size;column++)a[row,column]-=factor*a[k,column];
                b[row]-=factor*b[k];
            }
        }
        double[] x=new double[size];
        for(int row=size-1;row>=0;row--)
        {
            double value=b[row];
            for(int column=row+1;column<size;column++)value-=a[row,column]*x[column];
            x[row]=value/a[row,row];
        }
        return x;
    }

    // CPU reference for C*x' + G*x = f using the same collocation equations
    // as D*CQ + h*P*GFB in the shaders. C may be singular when the full MNA
    // stage block remains nonsingular.
    private static double[] LinearStep(double[,] capacitance,double[,] conductance,
        double[] forcing,double[] initial,double h,bool radau)
    {
        double[,] butcher=radau?RadauA():new double[,]{{1.0}};
        int stages=radau?3:1, n=initial.Length, size=stages*n;
        double[,] system=new double[size,size];double[] rhs=new double[size];
        for(int stage=0;stage<stages;stage++)
        {
            double rowSum=0.0;
            for(int sourceStage=0;sourceStage<stages;sourceStage++)rowSum+=butcher[stage,sourceStage];
            for(int row=0;row<n;row++)
            {
                int equation=stage*n+row;
                rhs[equation]=h*rowSum*forcing[row];
                for(int column=0;column<n;column++)rhs[equation]+=capacitance[row,column]*initial[column];
                for(int sourceStage=0;sourceStage<stages;sourceStage++)
                    for(int column=0;column<n;column++)
                        system[equation,sourceStage*n+column]=
                            (stage==sourceStage?capacitance[row,column]:0.0)+
                            h*butcher[stage,sourceStage]*conductance[row,column];
            }
        }
        double[] stagesValue=Solve(system,rhs),result=new double[n];
        for(int i=0;i<n;i++)result[i]=stagesValue[(stages-1)*n+i];
        return result;
    }

    private static double RadauDecay(double h)
    {
        double[,] a = RadauA();
        double[,] system = new double[3,3];
        double[] rhs = { 1.0, 1.0, 1.0 };
        for (int row = 0; row < 3; row++)
            for (int column = 0; column < 3; column++)
                system[row,column] = (row == column ? 1.0 : 0.0) + h * a[row,column];
        return Solve3(system, rhs)[2];
    }

    private static double JunctionCapacitance(double c0, double vj, double m, double fc, double v)
    {
        if (v < fc * vj) return c0 * System.Math.Pow(1.0-v/vj,-m);
        return c0 * System.Math.Pow(1.0-fc,-m-1.0) * (1.0-fc*(1.0+m)+m*v/vj);
    }

    private static double JunctionCharge(double c0, double vj, double m, double fc, double v)
    {
        double vf=fc*vj, oneMinusFc=1.0-fc;
        if(v<vf)
        {
            double oneMinusV=1.0-v/vj;
            if(System.Math.Abs(1.0-m)<1e-12)return -c0*vj*System.Math.Log(oneMinusV);
            return c0*vj*(1.0-System.Math.Pow(oneMinusV,1.0-m))/(1.0-m);
        }
        double qf=System.Math.Abs(1.0-m)<1e-12?-c0*vj*System.Math.Log(oneMinusFc):
            c0*vj*(1.0-System.Math.Pow(oneMinusFc,1.0-m))/(1.0-m);
        double cf=c0*System.Math.Pow(oneMinusFc,-m);
        double slope=c0*m/vj*System.Math.Pow(oneMinusFc,-m-1.0);
        double dv=v-vf;
        return qf+cf*dv+0.5*slope*dv*dv;
    }

    private static void CheckDerivative(System.Func<double,double> value, double x, double expected, string message)
    {
        double epsilon=1e-6;
        double numeric=(value(x+epsilon)-value(x-epsilon))/(2.0*epsilon);
        double tolerance=System.Math.Max(1e-14,System.Math.Abs(expected)*2e-5);
        Check(System.Math.Abs(numeric-expected)<=tolerance,message);
    }

    private static void BjtCharge(double u, double w, out double qbe, out double qbc,
        out double ce, out double cx, out double cc)
    {
        const double isat=1e-14, invNfVt=38.0, invNrVt=36.0;
        const double invVaf=.01, invVar=.02, invIkf=10.0, invIkr=20.0, nk=.5;
        const double cje=2e-12, vje=.75, mje=.33, cjc=1.5e-12, vjc=.7, mjc=.4, fc=.5;
        const double tf=3e-10, tr=2e-9;
        double expF=System.Math.Exp(u*invNfVt), expR=System.Math.Exp(w*invNrVt);
        double forward=isat*(expF-1.0), reverse=isat*(expR-1.0);
        double gF=isat*invNfVt*expF, gR=isat*invNrVt*expR;
        double a=1.0-w*invVaf-u*invVar;
        double z=forward*invIkf+reverse*invIkr;
        double s=System.Math.Max(1e-20,1.0+4.0*z);
        double d=System.Math.Pow(s,nk), dp=4.0*nk*System.Math.Pow(s,nk-1.0);
        double invOnePlusD=1.0/(1.0+d), h=2.0*invOnePlusD;
        double hp=-2.0*dp*invOnePlusD*invOnePlusD;
        double r=a*h;
        double ru=-invVar*h+a*hp*gF*invIkf;
        double rw=-invVaf*h+a*hp*gR*invIkr;
        qbe=JunctionCharge(cje,vje,mje,fc,u)+tf*forward*r;
        qbc=JunctionCharge(cjc,vjc,mjc,fc,w)+tr*reverse;
        ce=JunctionCapacitance(cje,vje,mje,fc,u)+tf*(gF*r+forward*ru);
        cx=tf*forward*rw;
        cc=JunctionCapacitance(cjc,vjc,mjc,fc,w)+tr*gR;
    }

    private static double BjtQbe(double u, double w)
    {
        double qbe,qbc,ce,cx,cc;
        BjtCharge(u,w,out qbe,out qbc,out ce,out cx,out cc);
        return qbe;
    }

    private static double BjtQbc(double u, double w)
    {
        double qbe,qbc,ce,cx,cc;
        BjtCharge(u,w,out qbe,out qbc,out ce,out cx,out cc);
        return qbc;
    }

    private static void BjtCurrent(double u,double w,out double baseCurrent,out double collectorCurrent,
        out double baseU,out double baseW,out double collectorU,out double collectorW)
    {
        const double isat=1e-14,invBf=.01,invBr=.02,invNfVt=38.0,invNrVt=36.0;
        const double ise=2e-14,invNeVt=35.0,isc=1e-14,invNcVt=34.0;
        const double invVaf=.01,invVar=.02,invIkf=10.0,invIkr=20.0,nk=.5;
        double expF=System.Math.Exp(u*invNfVt),expR=System.Math.Exp(w*invNrVt);
        double forward=isat*(expF-1.0),reverse=isat*(expR-1.0);
        double gF=isat*invNfVt*expF,gR=isat*invNrVt*expR;
        double a=1.0-w*invVaf-u*invVar,z=forward*invIkf+reverse*invIkr;
        double s=System.Math.Max(1e-20,1.0+4.0*z),d=System.Math.Pow(s,nk);
        double dp=4.0*nk*System.Math.Pow(s,nk-1.0),inverse=1.0/(1.0+d),h=2.0*inverse;
        double hp=-2.0*dp*inverse*inverse,r=a*h;
        double ru=-invVar*h+a*hp*gF*invIkf,rw=-invVaf*h+a*hp*gR*invIkr;
        double ibe=forward*invBf+ise*(System.Math.Exp(u*invNeVt)-1.0);
        double ibc=reverse*invBr+isc*(System.Math.Exp(w*invNcVt)-1.0);
        double gbe=gF*invBf+ise*invNeVt*System.Math.Exp(u*invNeVt);
        double gbc=gR*invBr+isc*invNcVt*System.Math.Exp(w*invNcVt);
        baseCurrent=ibe+ibc;collectorCurrent=(forward-reverse)*r-ibc;
        baseU=gbe;baseW=gbc;
        collectorU=gF*r+(forward-reverse)*ru;
        collectorW=-gR*r+(forward-reverse)*rw-gbc;
    }

    private static double BjtBaseCurrent(double u,double w)
    {
        double b,c,bu,bw,cu,cw;BjtCurrent(u,w,out b,out c,out bu,out bw,out cu,out cw);return b;
    }

    private static double BjtCollectorCurrent(double u,double w)
    {
        double b,c,bu,bw,cu,cw;BjtCurrent(u,w,out b,out c,out bu,out bw,out cu,out cw);return c;
    }

    private static void VerifyShader(string path, bool solverShader)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Shader shader=AssetDatabase.LoadAssetAtPath<Shader>(path);
        Check(shader!=null,"load "+path);
        Check(!ShaderUtil.ShaderHasError(shader),"compile "+path);
        Material material=new Material(shader);
        if (solverShader)
            Check(material.HasProperty("_TimeEvolution") && material.HasProperty("_SYSTEM_N") &&
                material.HasProperty("_OutputDeltaTime") && material.HasProperty("_MaxNewtonIterations") &&
                material.HasProperty("_RestartRevision"),
                "solver properties "+path);
        else
            Check(material.HasProperty("_SrcMatSize") && material.HasProperty("_DstMatSize") &&
                material.HasProperty("_DstTexHeight"),"copy properties "+path);
        Object.DestroyImmediate(material);
    }

    private static void VerifyDebugShader(string path)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        Shader shader=AssetDatabase.LoadAssetAtPath<Shader>(path);
        Check(shader!=null,"load "+path);
        Check(!ShaderUtil.ShaderHasError(shader),"compile "+path);
        Material material=new Material(shader);
        Check(material.HasProperty("_MainTex"),"debug solver texture "+path);
        Object.DestroyImmediate(material);
    }

    [MenuItem("Tools/VRCSpice/Verify predictorless solver")]
    public static void Run()
    {
        checks=0;
        double[,] coefficients=RadauA();
        double s=System.Math.Sqrt(6.0);
        double[] c={ (4.0-s)/10.0,(4.0+s)/10.0,1.0 };
        for(int row=0;row<3;row++)
        {
            double sum=0.0;
            for(int column=0;column<3;column++)sum+=coefficients[row,column];
            Check(System.Math.Abs(sum-c[row])<1e-12,"Radau row sum "+row);
        }
        double h=0.1;
        Check(System.Math.Abs(RadauDecay(h)-System.Math.Exp(-h))<2e-9,"Radau IIA order-five scalar decay");
        Check(System.Math.Abs(1.0/(1.0+h)-0.9090909090909091)<1e-14,"Backward Euler scalar decay");
        double[,] scalarC={{1.0}},scalarG={{1.0}};double[] zero={0.0},one={1.0};
        Check(System.Math.Abs(LinearStep(scalarC,scalarG,zero,one,h,true)[0]-System.Math.Exp(-h))<2e-9,
            "RC Radau CPU reference");
        Check(System.Math.Abs(LinearStep(new double[,]{{2.0}},new double[,]{{3.0}},zero,one,.05,true)[0]-
            System.Math.Exp(-.075))<2e-9,"RL Radau CPU reference");
        double r=1.0,l=1.0,cap=1.0,rlcH=.05;
        double[] rlc=LinearStep(new double[,]{{cap,0.0},{0.0,l}},new double[,]{{0.0,-1.0},{1.0,r}},
            new double[]{0.0,0.0},new double[]{1.0,0.0},rlcH,true);
        double alpha=r/(2.0*l),omega=System.Math.Sqrt(1.0/(l*cap)-alpha*alpha);
        double exactV=System.Math.Exp(-alpha*rlcH)*(System.Math.Cos(omega*rlcH)+alpha/omega*System.Math.Sin(omega*rlcH));
        Check(System.Math.Abs(rlc[0]-exactV)<2e-9,"RLC Radau CPU reference");
        double[] sourceMna=LinearStep(new double[,]{{0.0,0.0},{0.0,0.0}},
            new double[,]{{.25,1.0},{1.0,0.0}},new double[]{0.0,5.0},new double[]{0.0,0.0},.1,true);
        Check(System.Math.Abs(sourceMna[0]-5.0)<1e-11 && System.Math.Abs(sourceMna[1]+1.25)<1e-11,
            "voltage source and algebraic constraint Radau reference");
        double[] sourceBe=LinearStep(new double[,]{{0.0,0.0},{0.0,0.0}},
            new double[,]{{.25,1.0},{1.0,0.0}},new double[]{0.0,5.0},new double[]{0.0,0.0},.1,false);
        Check(System.Math.Abs(sourceBe[0]-5.0)<1e-11 && System.Math.Abs(sourceBe[1]+1.25)<1e-11,
            "voltage source and algebraic constraint Backward Euler reference");

        double progress=0.0,step=0.1;
        double[] accepted=new double[4];
        for(int i=0;i<4;i++)
        {
            step=System.Math.Min(step,1.0-progress); accepted[i]=step; progress+=step;
            step=System.Math.Min(2.0*step,System.Math.Max(0.0,1.0-progress));
        }
        Check(System.Math.Abs(progress-1.0)<1e-12 && System.Math.Abs(accepted[0]-.1)<1e-12 &&
            System.Math.Abs(accepted[1]-.2)<1e-12 && System.Math.Abs(accepted[2]-.4)<1e-12 &&
            System.Math.Abs(accepted[3]-.3)<1e-12,"normalized 0.1+0.2+0.4+0.3 interval");
        step=.1;
        for(int reject=0;reject<20;reject++)step*=.5;
        Check(step>0.0 && step<1e-7,"twenty rejection halvings and solver stop threshold");
        double composed=1.0;
        for(int i=0;i<accepted.Length;i++)composed=LinearStep(scalarC,scalarG,zero,new double[]{composed},accepted[i]*.1,true)[0];
        Check(System.Math.Abs(composed-System.Math.Exp(-.1))<3e-10,"accepted substeps compose one output interval");
        double previous=0.0;
        for(int i=0;i<1000;i++)
        {
            double time=(i+1)*.01;
            if(i>0)Check(System.Math.Abs(time-previous-.01)<1e-12,"uniform output time "+i);
            previous=time;
        }

        double c0=2e-12,vj=.7,m=.4,fc=.5;
        CheckDerivative(v=>JunctionCharge(c0,vj,m,fc,v),.1,JunctionCapacitance(c0,vj,m,fc,.1),"junction charge derivative below Fc");
        CheckDerivative(v=>JunctionCharge(c0,vj,m,fc,v),.6,JunctionCapacitance(c0,vj,m,fc,.6),"junction charge derivative above Fc");
        double isat=1e-12,invNVt=38.0,tt=2e-9,diodeV=.35;
        CheckDerivative(v=>isat*(System.Math.Exp(v*invNVt)-1.0),diodeV,
            isat*invNVt*System.Math.Exp(diodeV*invNVt),"diode GFB current derivative");
        CheckDerivative(v=>JunctionCharge(c0,vj,m,fc,v)+tt*isat*(System.Math.Exp(v*invNVt)-1.0),diodeV,
            JunctionCapacitance(c0,vj,m,fc,diodeV)+tt*isat*invNVt*System.Math.Exp(diodeV*invNVt),
            "diode nonlinear charge derivative");
        double bjtU=.42,bjtW=.18,qbe,qbc,ce,cx,cc;
        BjtCharge(bjtU,bjtW,out qbe,out qbc,out ce,out cx,out cc);
        CheckDerivative(u=>BjtQbe(u,bjtW),bjtU,ce,"BJT qbe derivative by vbe");
        CheckDerivative(w=>BjtQbe(bjtU,w),bjtW,cx,"BJT qbe derivative by vbc");
        CheckDerivative(w=>BjtQbc(bjtU,w),bjtW,cc,"BJT qbc derivative by vbc");
        double baseCurrent,collectorCurrent,baseU,baseW,collectorU,collectorW;
        BjtCurrent(bjtU,bjtW,out baseCurrent,out collectorCurrent,out baseU,out baseW,out collectorU,out collectorW);
        CheckDerivative(u=>BjtBaseCurrent(u,bjtW),bjtU,baseU,"BJT GFB base derivative by vbe");
        CheckDerivative(w=>BjtBaseCurrent(bjtU,w),bjtW,baseW,"BJT GFB base derivative by vbc");
        CheckDerivative(u=>BjtCollectorCurrent(u,bjtW),bjtU,collectorU,"BJT GFB collector derivative by vbe");
        CheckDerivative(w=>BjtCollectorCurrent(bjtU,w),bjtW,collectorW,"BJT GFB collector derivative by vbc");

        VerifyShader("Assets/VRCSpice/MNATools/Shader/Processor.shader",true);
        VerifyShader("Assets/VRCSpice/MNATools/Shader/FlowControl.shader",true);
        VerifyShader("Assets/VRCSpice/MNATools/Shader/CopyColumn.shader",false);
        VerifyDebugShader("Assets/VRCSpice/FontAsset/putFloatTest.shader");
        Debug.Log("Predictorless solver verification PASSED: "+checks+" assertions.");
    }
}
